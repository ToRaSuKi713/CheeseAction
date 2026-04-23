using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ChzzkLoginManager : MonoBehaviour
{
    [SerializeField] private string backendBaseUrl = "https://chzzk-auth-worker.rang2zoa.workers.dev";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private bool allowBackendUrlOverride = true;
#else
    [SerializeField] private bool allowBackendUrlOverride = false;
#endif
    [SerializeField] private int requestTimeoutSeconds = 15;
    [SerializeField] private int refreshLeewaySeconds = 60;

    private readonly object accessTokenLock = new object();
    private AppAuthApiClient apiClient;
    private LocalOAuthCallbackListener callbackListener;
    private Task<string> inFlightAccessTokenTask;
    private string installId;
    private string appSessionToken;
    private string accessToken;
    private DateTimeOffset accessTokenExpiresAtUtc = DateTimeOffset.MinValue;
    private string channelId;
    private bool loginInProgress;
    private string statusText = "로그인 필요";

    public event Action OnLoginCompleted;
    public event Action OnLogoutCompleted;
    public event Action<string> OnForcedLogout;
    public event Action<string> OnStatusChanged;

    public string StatusText => statusText;
    public string BackendBaseUrl => backendBaseUrl;
    public bool CanOverrideBackendUrl => allowBackendUrlOverride;
    public string InstallId => installId;
    public string ChannelId => channelId;
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(appSessionToken);
    public bool IsBusy => loginInProgress;

    void Awake()
    {
        AuthStorage.ClearLegacySensitivePrefs();
        installId = AuthStorage.GetOrCreateInstallId();
        apiClient = new AppAuthApiClient(backendBaseUrl, requestTimeoutSeconds);
        callbackListener = new LocalOAuthCallbackListener();
        EnsureSessionClient();

        try
        {
            LoadStoredState();
            SetStatus(IsLoggedIn ? "로그인 완료" : "로그인 필요");
        }
        catch (AuthStorageException ex) when (ex.FailureKind == AuthStorageFailureKind.TokenDecryptFailed)
        {
            ForceLogout("토큰 복호화 실패");
        }
    }

    void Start()
    {
        if (IsLoggedIn)
            _ = RestoreSessionAsync();
    }

    void OnDestroy()
    {
        callbackListener?.Dispose();
    }

    public void SetBackendBaseUrl(string value)
    {
        if (!allowBackendUrlOverride)
            return;

        backendBaseUrl = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        AuthStorage.SetBackendBaseUrl(backendBaseUrl);
        apiClient?.SetBackendBaseUrl(backendBaseUrl);
    }

    public void BeginLogin()
    {
        if (loginInProgress)
            return;

        _ = BeginLoginAsync();
    }

    public void BeginLogout()
    {
        _ = LogoutAsync();
    }

    public void InvalidateAccessToken()
    {
        accessToken = string.Empty;
        accessTokenExpiresAtUtc = DateTimeOffset.MinValue;
    }

    public async Task<bool> EnsureLoggedInAsync()
    {
        if (!IsLoggedIn)
            return false;

        try
        {
            await GetValidAccessTokenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetValidAccessTokenAsync()
    {
        if (!IsLoggedIn)
            throw new Exception("로그인이 필요합니다.");

        if (HasUsableAccessToken())
            return accessToken;

        Task<string> task;
        lock (accessTokenLock)
        {
            if (inFlightAccessTokenTask == null || inFlightAccessTokenTask.IsCompleted)
                inFlightAccessTokenTask = RefreshAccessTokenAsync();

            task = inFlightAccessTokenTask;
        }

        try
        {
            return await task;
        }
        finally
        {
            lock (accessTokenLock)
            {
                if (inFlightAccessTokenTask == task && task.IsCompleted)
                    inFlightAccessTokenTask = null;
            }
        }
    }

    private async Task BeginLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(backendBaseUrl))
        {
            SetStatus("로그인 실패: 백엔드 주소 필요");
            return;
        }

        loginInProgress = true;
        try
        {
            installId = AuthStorage.GetOrCreateInstallId();
            apiClient.SetBackendBaseUrl(backendBaseUrl);

            SetStatus("브라우저에서 로그인 진행 중");
            AuthStartResponse startResponse = await apiClient.StartAuthAsync(installId, CancellationToken.None);

            if (!callbackListener.TryStart(out string listenerError))
                throw new Exception(listenerError);

            Application.OpenURL(startResponse.authUrl);
            SetStatus("콜백 대기 중");

            LocalOAuthCallbackListener.CallbackResult callback =
                await callbackListener.WaitForCallbackAsync(Mathf.Max(30, startResponse.expiresIn + 10));

            SetStatus("콜백 수신됨");

            if (!string.IsNullOrWhiteSpace(callback.Error))
                throw new Exception("인증 콜백 오류");

            if (string.IsNullOrWhiteSpace(callback.Code) || string.IsNullOrWhiteSpace(callback.State))
                throw new Exception("인증 코드가 비어 있습니다.");

            SetStatus("토큰 교환 중");
            AuthFinishResponse finishResponse = await apiClient.FinishAuthAsync(
                installId,
                startResponse.loginTicket,
                callback.Code,
                callback.State,
                CancellationToken.None
            );

            appSessionToken = finishResponse.appSessionToken ?? string.Empty;
            accessToken = finishResponse.accessToken ?? string.Empty;
            accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Mathf.Max(60, finishResponse.expiresIn));
            channelId = finishResponse.channelId ?? string.Empty;

            try
            {
                AuthStorage.SaveSession(appSessionToken, channelId);
            }
            catch (AuthStorageException ex) when (ex.FailureKind == AuthStorageFailureKind.TokenSaveFailed)
            {
                ForceLogout("토큰 저장 실패");
                return;
            }

            SetStatus("로그인 완료");
            OnLoginCompleted?.Invoke();
        }
        catch (AppAuthApiException ex) when (ex.IsUnauthorized || ex.StatusCode == 400)
        {
            ForceLogout("세션 만료");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ChzzkLoginManager] Login failed: " + SensitiveLogMasker.Mask(ex.Message));
            SetStatus("로그인 실패: " + GetUserFacingError(ex));
        }
        finally
        {
            callbackListener.Stop();
            loginInProgress = false;
        }
    }

    private async Task RestoreSessionAsync()
    {
        try
        {
            SetStatus("세션 복구 중");
            await GetValidAccessTokenAsync();
            SetStatus("로그인 완료");
            OnLoginCompleted?.Invoke();
        }
        catch (AppAuthApiException ex) when (ex.IsUnauthorized)
        {
            ForceLogout("세션 만료");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ChzzkLoginManager] Session restore failed: " + SensitiveLogMasker.Mask(ex.Message));
            ForceLogout("세션 복구 실패");
        }
    }

    private async Task<string> RefreshAccessTokenAsync()
    {
        apiClient.SetBackendBaseUrl(backendBaseUrl);

        try
        {
            SessionAccessTokenResponse response = await apiClient.GetAccessTokenAsync(appSessionToken, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(response.accessToken))
            {
                ForceLogout("세션 만료");
                throw new Exception("Access Token이 비어 있습니다.");
            }

            accessToken = response.accessToken;
            accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Mathf.Max(60, response.expiresIn));
            return accessToken;
        }
        catch (AppAuthApiException ex) when (ex.IsUnauthorized || ex.StatusCode == 400)
        {
            ForceLogout("세션 만료");
            throw;
        }
    }

    private async Task LogoutAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(appSessionToken) && !string.IsNullOrWhiteSpace(backendBaseUrl))
            {
                apiClient.SetBackendBaseUrl(backendBaseUrl);
                await apiClient.LogoutAsync(appSessionToken, CancellationToken.None);
            }
        }
        catch (AppAuthApiException ex) when (ex.IsUnauthorized)
        {
            ForceLogout("세션 만료");
            return;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ChzzkLoginManager] Logout request failed: " + SensitiveLogMasker.Mask(ex.Message));
        }
        finally
        {
            ClearRuntimeSession();
            AuthStorage.ClearSession();
            SetStatus("로그인 필요");
            OnLogoutCompleted?.Invoke();
        }
    }

    private void ForceLogout(string reason)
    {
        Debug.LogWarning("[ChzzkLoginManager] Forced logout: " + SensitiveLogMasker.Mask(reason));
        callbackListener?.Stop();
        loginInProgress = false;
        ClearRuntimeSession();
        AuthStorage.ClearSession();
        SetStatus("로그인 필요");
        OnForcedLogout?.Invoke(reason);
        OnLogoutCompleted?.Invoke();
    }

    private void ClearRuntimeSession()
    {
        appSessionToken = string.Empty;
        accessToken = string.Empty;
        accessTokenExpiresAtUtc = DateTimeOffset.MinValue;
        channelId = string.Empty;
    }

    private bool HasUsableAccessToken()
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        return accessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(refreshLeewaySeconds);
    }

    private void LoadStoredState()
    {
        StoredAuthSession session = AuthStorage.LoadSession();
        if (!string.IsNullOrWhiteSpace(session.installId))
            installId = session.installId;

        if (allowBackendUrlOverride && !string.IsNullOrWhiteSpace(session.backendBaseUrl))
            backendBaseUrl = session.backendBaseUrl;

        appSessionToken = session.appSessionToken ?? string.Empty;
        accessToken = string.Empty;
        accessTokenExpiresAtUtc = DateTimeOffset.MinValue;
        channelId = session.channelId ?? string.Empty;
    }

    private void SetStatus(string value)
    {
        statusText = string.IsNullOrWhiteSpace(value) ? "로그인 필요" : value;
        OnStatusChanged?.Invoke(statusText);
    }

    private string GetUserFacingError(Exception ex)
    {
        if (ex is TimeoutException)
            return "시간 초과";

        string message = ex.Message ?? string.Empty;
        if (message.Contains("포트"))
            return "콜백 포트 사용 중";
        if (message.Contains("네트워크"))
            return "네트워크 오류";
        if (message.Contains("서버"))
            return "서버 응답 오류";
        if (message.Contains("백엔드"))
            return "백엔드 주소 오류";
        return "처리에 실패했습니다";
    }

    private void EnsureSessionClient()
    {
        if (UnityEngine.Object.FindAnyObjectByType<ChzzkSessionClient>() != null)
            return;

        GameObject sessionClientObject = new GameObject("ChzzkSessionClient");
        sessionClientObject.AddComponent<ChzzkSessionClient>();
    }
}
