using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class ChzzkSessionApiException : Exception
{
    public long StatusCode { get; }
    public bool IsUnauthorized => StatusCode == 401;

    public ChzzkSessionApiException(string message, long statusCode = 0) : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed class ChzzkSessionApiClient
{
    private const string OpenApiBaseUrl = "https://openapi.chzzk.naver.com";
    private readonly int timeoutSeconds;

    public ChzzkSessionApiClient(int timeoutSeconds = 15)
    {
        this.timeoutSeconds = Mathf.Clamp(timeoutSeconds, 5, 60);
    }

    public async Task<ChzzkSessionAuthResponse> CreateUserSessionAsync(string accessToken, CancellationToken cancellationToken)
    {
        string responseText = await SendAuthorizedAsync(UnityWebRequest.kHttpVerbGET, "/open/v1/sessions/auth", accessToken, cancellationToken);
        string url = ExtractJsonString(responseText, "url");
        if (string.IsNullOrWhiteSpace(url))
            throw new ChzzkSessionApiException("세션 URL을 받지 못했습니다.");

        return new ChzzkSessionAuthResponse { url = url };
    }

    public Task SubscribeChatAsync(string accessToken, string sessionKey, CancellationToken cancellationToken)
    {
        return SendAuthorizedAsync(UnityWebRequest.kHttpVerbPOST,
            "/open/v1/sessions/events/subscribe/chat?sessionKey=" + Uri.EscapeDataString(sessionKey),
            accessToken,
            cancellationToken);
    }

    public Task SubscribeDonationAsync(string accessToken, string sessionKey, CancellationToken cancellationToken)
    {
        return SendAuthorizedAsync(UnityWebRequest.kHttpVerbPOST,
            "/open/v1/sessions/events/subscribe/donation?sessionKey=" + Uri.EscapeDataString(sessionKey),
            accessToken,
            cancellationToken);
    }

    private async Task<string> SendAuthorizedAsync(string method, string path, string accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ChzzkSessionApiException("Access Token이 비어 있습니다.");

        using (UnityWebRequest request = new UnityWebRequest(OpenApiBaseUrl + path, method))
        {
            request.timeout = timeoutSeconds;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);

            if (method == UnityWebRequest.kHttpVerbPOST)
            {
                request.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
                request.SetRequestHeader("Content-Type", "application/json");
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(50, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new ChzzkSessionApiException("치지직 Open API 연결에 실패했습니다.");

            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                if (request.responseCode == 401)
                    throw new ChzzkSessionApiException("치지직 세션 인증이 만료되었습니다.", 401);

                throw new ChzzkSessionApiException("치지직 세션 API 오류가 발생했습니다. (" + request.responseCode + ")", request.responseCode);
            }

            if (request.result == UnityWebRequest.Result.DataProcessingError)
                throw new ChzzkSessionApiException("치지직 세션 응답을 처리하지 못했습니다.");

            return request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        }
    }

    private static string ExtractJsonString(string raw, string key)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(key))
            return string.Empty;

        Match match = Regex.Match(raw, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
