using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using UnityEngine;
using Object = UnityEngine.Object;

public class ChzzkSessionClient : MonoBehaviour
{
    [SerializeField] private ChzzkLoginManager loginManager;
    [SerializeField] private ChzzkRealtimeRouter realtimeRouter;
    [SerializeField] private bool autoConnectOnLogin = true;
    [SerializeField] private float reconnectDelaySeconds = 3f;
    [SerializeField] private float maxReconnectDelaySeconds = 15f;
    [SerializeField] private int sessionConnectTimeoutSeconds = 10;

    private readonly object connectLock = new object();
    private CancellationTokenSource lifetimeCts;
    private ChzzkSessionApiClient apiClient;
    private SocketIOUnity socket;
    private Task activeConnectTask;
    private bool reconnectScheduled;
    private bool intentionalDisconnect;
    private string sessionKey = string.Empty;
    private string statusText = "세션 대기 중";

    public string StatusText => statusText;
    public string SessionKey => sessionKey;
    public bool IsConnected => !string.IsNullOrWhiteSpace(sessionKey) && socket != null && socket.Connected;

    void Awake()
    {
        if (loginManager == null)
            loginManager = Object.FindAnyObjectByType<ChzzkLoginManager>();

        if (realtimeRouter == null)
            realtimeRouter = Object.FindAnyObjectByType<ChzzkRealtimeRouter>();

        if (realtimeRouter == null)
        {
            GameObject routerObject = new GameObject("ChzzkRealtimeRouter");
            realtimeRouter = routerObject.AddComponent<ChzzkRealtimeRouter>();
        }

        apiClient = new ChzzkSessionApiClient();
        lifetimeCts = new CancellationTokenSource();
    }

    void OnEnable()
    {
        if (loginManager == null)
            loginManager = Object.FindAnyObjectByType<ChzzkLoginManager>();

        if (loginManager == null)
            return;

        loginManager.OnLoginCompleted += HandleLoginCompleted;
        loginManager.OnLogoutCompleted += HandleLogoutCompleted;
        loginManager.OnForcedLogout += HandleForcedLogout;
    }

    void Start()
    {
        if (autoConnectOnLogin)
            _ = BootstrapAsync();
    }

    void OnDisable()
    {
        if (loginManager != null)
        {
            loginManager.OnLoginCompleted -= HandleLoginCompleted;
            loginManager.OnLogoutCompleted -= HandleLogoutCompleted;
            loginManager.OnForcedLogout -= HandleForcedLogout;
        }
    }

    async void OnDestroy()
    {
        lifetimeCts?.Cancel();
        await DisconnectSocketAsync();
        lifetimeCts?.Dispose();
    }

    public Task EnsureConnectedAsync()
    {
        if (loginManager == null)
            return Task.CompletedTask;

        lock (connectLock)
        {
            if (activeConnectTask == null || activeConnectTask.IsCompleted)
                activeConnectTask = ConnectAndSubscribeAsync(lifetimeCts.Token);

            return activeConnectTask;
        }
    }

    private async Task BootstrapAsync()
    {
        if (loginManager == null || !loginManager.IsLoggedIn)
            return;

        bool restored = await loginManager.EnsureLoggedInAsync();
        if (!restored)
            return;

        await EnsureConnectedAsync();
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        try
        {
            intentionalDisconnect = false;
            reconnectScheduled = false;
            sessionKey = string.Empty;
            SetStatus("치지직 세션 연결 중");

            string accessToken = await loginManager.GetValidAccessTokenAsync();
            ChzzkSessionAuthResponse sessionResponse = await apiClient.CreateUserSessionAsync(accessToken, cancellationToken);

            await DisconnectSocketAsync();

            TaskCompletionSource<string> sessionKeySource = new TaskCompletionSource<string>();
            CreateSocket(sessionResponse.url, sessionKeySource);

            await socket.ConnectAsync();

            Task completed = await Task.WhenAny(
                sessionKeySource.Task,
                Task.Delay(TimeSpan.FromSeconds(sessionConnectTimeoutSeconds), cancellationToken));

            if (completed != sessionKeySource.Task)
                throw new Exception("세션 연결 시간이 초과되었습니다.");

            sessionKey = sessionKeySource.Task.Result;

            await apiClient.SubscribeChatAsync(accessToken, sessionKey, cancellationToken);
            await apiClient.SubscribeDonationAsync(accessToken, sessionKey, cancellationToken);

            SetStatus("치지직 실시간 연결 완료");
            Debug.Log("[ChzzkSessionClient] CHZZK realtime connected.");
        }
        catch (ChzzkSessionApiException ex) when (ex.IsUnauthorized)
        {
            loginManager.InvalidateAccessToken();
            ScheduleReconnect();
        }
        catch (AppAuthApiException ex) when (ex.IsUnauthorized || ex.StatusCode == 400)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ChzzkSessionClient] Connect failed: " + SensitiveLogMasker.Mask(ex.Message));
            ScheduleReconnect();
        }
    }

    private async void HandleLoginCompleted()
    {
        if (!autoConnectOnLogin)
            return;

        await EnsureConnectedAsync();
    }

    private async void HandleLogoutCompleted()
    {
        await DisconnectInternalAsync(true);
    }

    private async void HandleForcedLogout(string reason)
    {
        await DisconnectInternalAsync(true);
    }

    private void HandleSystemMessage(ChzzkSystemMessage message)
    {
        if (message == null)
            return;

        if (string.Equals(message.type, "connected", StringComparison.OrdinalIgnoreCase) &&
            message.data != null &&
            !string.IsNullOrWhiteSpace(message.data.sessionKey))
        {
            sessionKey = message.data.sessionKey;
        }

        if (string.Equals(message.type, "revoked", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[ChzzkSessionClient] Session revoked. Reconnecting.");
            loginManager.InvalidateAccessToken();
            ScheduleReconnect();
        }
    }

    private void HandleChatMessage(ChzzkChatMessage message)
    {
        realtimeRouter?.RouteChat(message);
    }

    private void HandleDonationMessage(ChzzkDonationMessage message)
    {
        realtimeRouter?.RouteDonation(message);
    }

    private void HandleSocketClosed(string reason)
    {
        sessionKey = string.Empty;
        SetStatus("치지직 세션 재연결 대기");

        if (!intentionalDisconnect)
            ScheduleReconnect();
    }

    private void HandleSocketError(string message)
    {
        Debug.LogWarning("[ChzzkSessionClient] Socket error: " + SensitiveLogMasker.Mask(message));
    }

    private void CreateSocket(string sessionUrl, TaskCompletionSource<string> sessionKeySource)
    {
        Uri uri = new Uri(sessionUrl);
        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Transport = TransportProtocol.WebSocket,
            EIO = EngineIO.V3,
            Reconnection = false,
            ReconnectionDelay = reconnectDelaySeconds * 1000f,
            ReconnectionDelayMax = Mathf.RoundToInt(maxReconnectDelaySeconds * 1000f),
            ConnectionTimeout = TimeSpan.FromSeconds(sessionConnectTimeoutSeconds),
            Query = ParseQuery(uri.Query)
        });
        socket.JsonSerializer = new NewtonsoftJsonSerializer();

        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("[ChzzkSessionClient] Socket connected.");
        };
        socket.OnDisconnected += (sender, e) =>
        {
            HandleSocketClosed(e);
        };
        socket.OnError += (sender, e) =>
        {
            HandleSocketError(e);
        };
        socket.OnReconnectAttempt += (sender, e) =>
        {
            SetStatus("치지직 세션 재연결 중");
        };

        RegisterSystemHandler("SYSTEM", sessionKeySource);
        RegisterSystemHandler("system", sessionKeySource);
        RegisterChatHandler("CHAT");
        RegisterChatHandler("chat");
        RegisterDonationHandler("DONATION");
        RegisterDonationHandler("donation");
    }

    private void RegisterSystemHandler(string eventName, TaskCompletionSource<string> sessionKeySource)
    {
        socket.OnUnityThread(eventName, response =>
        {
            if (RuntimeLogSettings.LogSocketPayloads)
                Debug.Log("[ChzzkSessionClient] SYSTEM payload: " + RuntimeLogSettings.MaskAndCompact(response.ToString()));

            ChzzkSystemMessage message = ParseSocketPayload<ChzzkSystemMessage>(response);
            HandleSystemMessage(message);

            if (message != null &&
                string.Equals(message.type, "connected", StringComparison.OrdinalIgnoreCase) &&
                message.data != null &&
                !string.IsNullOrWhiteSpace(message.data.sessionKey))
            {
                sessionKeySource.TrySetResult(message.data.sessionKey);
            }
        });
    }

    private void RegisterChatHandler(string eventName)
    {
        socket.OnUnityThread(eventName, response =>
        {
            if (RuntimeLogSettings.LogSocketPayloads)
                Debug.Log("[ChzzkSessionClient] CHAT payload: " + RuntimeLogSettings.MaskAndCompact(response.ToString()));

            ChzzkChatMessage message = ParseSocketPayload<ChzzkChatMessage>(response);
            HandleChatMessage(message);
        });
    }

    private void RegisterDonationHandler(string eventName)
    {
        socket.OnUnityThread(eventName, response =>
        {
            if (RuntimeLogSettings.LogSocketPayloads)
                Debug.Log("[ChzzkSessionClient] DONATION payload: " + RuntimeLogSettings.MaskAndCompact(response.ToString()));

            ChzzkDonationMessage message = ParseSocketPayload<ChzzkDonationMessage>(response);
            HandleDonationMessage(message);
        });
    }

    private void ScheduleReconnect()
    {
        if (reconnectScheduled || intentionalDisconnect || loginManager == null || !loginManager.IsLoggedIn)
            return;

        reconnectScheduled = true;
        _ = ReconnectAsync();
    }

    private async Task ReconnectAsync()
    {
        try
        {
            float delay = Mathf.Clamp(reconnectDelaySeconds, 1f, maxReconnectDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delay), lifetimeCts.Token);
            await DisconnectInternalAsync(false);
            await EnsureConnectedAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            reconnectScheduled = false;
        }
    }

    private async Task DisconnectInternalAsync(bool markIntentional)
    {
        intentionalDisconnect = markIntentional;
        sessionKey = string.Empty;
        SetStatus("세션 대기 중");
        await DisconnectSocketAsync();
    }

    private void SetStatus(string value)
    {
        statusText = string.IsNullOrWhiteSpace(value) ? "세션 대기 중" : value;
    }

    private async Task DisconnectSocketAsync()
    {
        if (socket == null)
            return;

        try
        {
            if (socket.Connected)
                await socket.DisconnectAsync();
        }
        catch
        {
        }

        socket.Dispose();
        socket = null;
    }

    private Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string raw = string.IsNullOrWhiteSpace(query) ? string.Empty : query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        string[] pairs = raw.Split('&');
        for (int i = 0; i < pairs.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(pairs[i]))
                continue;

            string[] parts = pairs[i].Split(new[] { '=' }, 2);
            string key = Uri.UnescapeDataString(parts[0]);
            string value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private T ParseSocketPayload<T>(SocketIOResponse response) where T : class
    {
        try
        {
            return response.GetValue<T>();
        }
        catch
        {
            string rawText = response.GetValue().GetRawText();
            if (string.IsNullOrWhiteSpace(rawText))
                return null;

            try
            {
                string nestedJson = JsonConvert.DeserializeObject<string>(rawText);
                if (!string.IsNullOrWhiteSpace(nestedJson))
                    return JsonConvert.DeserializeObject<T>(nestedJson);
            }
            catch
            {
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(rawText);
            }
            catch
            {
                return null;
            }
        }
    }
}
