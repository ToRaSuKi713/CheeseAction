using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class LocalOAuthCallbackListener : IDisposable
{
    public const string CallbackUrl = "http://127.0.0.1:45678/chzzk/callback/";
    private static readonly string[] Prefixes =
    {
        "http://127.0.0.1:45678/",
        "http://localhost:45678/"
    };

    public sealed class CallbackResult
    {
        public string Code;
        public string State;
        public string Error;
        public string RawUrl;
    }

    private HttpListener listener;
    private CancellationTokenSource cancellationTokenSource;
    private TaskCompletionSource<CallbackResult> callbackSource;

    public bool IsListening { get; private set; }

    public bool TryStart(out string errorMessage)
    {
        errorMessage = string.Empty;
        Stop();

        try
        {
            listener = new HttpListener();
            for (int i = 0; i < Prefixes.Length; i++)
                listener.Prefixes.Add(Prefixes[i]);

            listener.Start();
            cancellationTokenSource = new CancellationTokenSource();
            callbackSource = new TaskCompletionSource<CallbackResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            IsListening = true;
            _ = Task.Run(() => ListenLoop(cancellationTokenSource.Token), cancellationTokenSource.Token);
            Debug.Log("[LocalOAuthCallbackListener] Started");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LocalOAuthCallbackListener] Start failed: " + SensitiveLogMasker.Mask(ex.ToString()));
            errorMessage = ex is HttpListenerException ? "콜백 포트를 열 수 없습니다." : "콜백 리스너 시작에 실패했습니다.";
            Stop();
            return false;
        }
    }

    public async Task<CallbackResult> WaitForCallbackAsync(int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        if (callbackSource == null)
            throw new InvalidOperationException("콜백 리스너가 시작되지 않았습니다.");

        Task completed = await Task.WhenAny(
            callbackSource.Task,
            Task.Delay(TimeSpan.FromSeconds(Math.Max(10, timeoutSeconds)), cancellationToken)
        );

        if (completed != callbackSource.Task)
            throw new TimeoutException("로그인 시간이 초과되었습니다.");

        return await callbackSource.Task;
    }

    public void Stop()
    {
        IsListening = false;

        try { cancellationTokenSource?.Cancel(); } catch { }

        try
        {
            if (listener != null)
            {
                if (listener.IsListening)
                    listener.Stop();
                listener.Close();
            }
        }
        catch { }

        listener = null;
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private void ListenLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && listener != null && listener.IsListening)
            {
                HttpListenerContext context = listener.GetContext();
                string rawUrl = context.Request.RawUrl ?? string.Empty;
                string path = context.Request.Url != null ? context.Request.Url.AbsolutePath : string.Empty;

                Debug.Log("[LocalOAuthCallbackListener] Request: " + SensitiveLogMasker.Mask(rawUrl));

                if (!IsCallbackPath(path))
                {
                    WriteNotFoundResponse(context.Response);
                    continue;
                }

                CallbackResult result = BuildResult(context.Request);
                WriteHtmlResponse(context.Response, string.IsNullOrWhiteSpace(result.Error));
                callbackSource.TrySetResult(result);
                break;
            }
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("[LocalOAuthCallbackListener] Listen failed: " + SensitiveLogMasker.Mask(ex.ToString()));
                callbackSource?.TrySetException(ex);
            }
        }
        finally
        {
            Stop();
        }
    }

    private bool IsCallbackPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalized = path.Trim().TrimEnd('/').ToLowerInvariant();
        return normalized == "/chzzk/callback" || normalized == "/callback";
    }

    private CallbackResult BuildResult(HttpListenerRequest request)
    {
        return new CallbackResult
        {
            Code = request.QueryString["code"] ?? string.Empty,
            State = request.QueryString["state"] ?? string.Empty,
            Error = request.QueryString["error"] ?? request.QueryString["error_description"] ?? string.Empty,
            RawUrl = request.RawUrl ?? string.Empty
        };
    }

    private void WriteHtmlResponse(HttpListenerResponse response, bool success)
    {
        string html = success
            ? "<html><body style='font-family:sans-serif;padding:24px;'><h2>로그인이 완료되었습니다.</h2><p>앱으로 돌아가세요.</p></body></html>"
            : "<html><body style='font-family:sans-serif;padding:24px;'><h2>로그인에 실패했습니다.</h2><p>앱으로 돌아가 다시 시도하세요.</p></body></html>";

        byte[] data = Encoding.UTF8.GetBytes(html);
        response.StatusCode = success ? 200 : 400;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.Length;

        using (Stream output = response.OutputStream)
            output.Write(data, 0, data.Length);
    }

    private void WriteNotFoundResponse(HttpListenerResponse response)
    {
        byte[] data = Encoding.UTF8.GetBytes("<html><body>Not Found</body></html>");
        response.StatusCode = 404;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.Length;

        using (Stream output = response.OutputStream)
            output.Write(data, 0, data.Length);
    }
}
