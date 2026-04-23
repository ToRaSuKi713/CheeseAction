using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AppAuthApiException : Exception
{
    public long StatusCode { get; }
    public bool IsUnauthorized => StatusCode == 401;

    public AppAuthApiException(string message, long statusCode = 0) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class AppAuthApiClient
{
    private string backendBaseUrl;
    private readonly int timeoutSeconds;

    public AppAuthApiClient(string backendBaseUrl, int timeoutSeconds = 15)
    {
        this.backendBaseUrl = NormalizeBaseUrl(backendBaseUrl);
        this.timeoutSeconds = Mathf.Clamp(timeoutSeconds, 5, 60);
    }

    public void SetBackendBaseUrl(string value)
    {
        backendBaseUrl = NormalizeBaseUrl(value);
    }

    public Task<AuthStartResponse> StartAuthAsync(string installId, CancellationToken cancellationToken)
    {
        return PostJsonAsync<AuthStartRequest, AuthStartResponse>("/auth/start", new AuthStartRequest { installId = installId }, cancellationToken);
    }

    public Task<AuthFinishResponse> FinishAuthAsync(string installId, string loginTicket, string code, string state, CancellationToken cancellationToken)
    {
        return PostJsonAsync<AuthFinishRequest, AuthFinishResponse>("/auth/finish", new AuthFinishRequest
        {
            installId = installId,
            loginTicket = loginTicket,
            code = code,
            state = state
        }, cancellationToken);
    }

    public Task<SessionAccessTokenResponse> GetAccessTokenAsync(string appSessionToken, CancellationToken cancellationToken)
    {
        return PostJsonAsync<SessionAccessTokenRequest, SessionAccessTokenResponse>("/session/access-token", new SessionAccessTokenRequest
        {
            appSessionToken = appSessionToken
        }, cancellationToken);
    }

    public Task<SessionLogoutResponse> LogoutAsync(string appSessionToken, CancellationToken cancellationToken)
    {
        return PostJsonAsync<SessionLogoutRequest, SessionLogoutResponse>("/session/logout", new SessionLogoutRequest
        {
            appSessionToken = appSessionToken
        }, cancellationToken);
    }

    private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
        where TResponse : AuthApiResponseBase
    {
        if (string.IsNullOrWhiteSpace(backendBaseUrl))
            throw new AppAuthApiException("백엔드 주소가 비어 있습니다.");

        string url = backendBaseUrl + path;
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.timeout = timeoutSeconds;
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(50, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new AppAuthApiException("네트워크 연결에 실패했습니다.");

            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                if (request.responseCode == 401)
                    throw new AppAuthApiException("세션이 만료되었습니다.", 401);

                throw new AppAuthApiException($"서버 오류가 발생했습니다. ({request.responseCode})", request.responseCode);
            }

            if (request.result == UnityWebRequest.Result.DataProcessingError)
                throw new AppAuthApiException("서버 응답을 처리하지 못했습니다.");

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (string.IsNullOrWhiteSpace(responseText))
                throw new AppAuthApiException("서버 응답이 비어 있습니다.");

            TResponse response;
            try
            {
                response = JsonUtility.FromJson<TResponse>(responseText);
            }
            catch
            {
                response = null;
            }

            if (response == null)
                throw new AppAuthApiException("서버 응답 형식이 올바르지 않습니다.");

            if (!response.ok)
                throw new AppAuthApiException("백엔드 요청이 거부되었습니다.");

            return response;
        }
    }

    private static string NormalizeBaseUrl(string value)
    {
        string url = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (url.EndsWith("/"))
            url = url.Substring(0, url.Length - 1);
        return url;
    }
}
