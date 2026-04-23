using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UdpEventReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int port = 9000;
    public bool logReceivedMessages = true;
    [Min(128)] public int maxMessageBytes = 4096;
    [Min(1)] public int maxQueuedMessages = 128;
    [Min(1)] public int maxMessagesPerFrame = 12;
    [Min(1)] public int maxMessagesPerSecond = 60;

    public string LastStatus { get; private set; } = "Idle";
    public string LastReceivedEventType { get; private set; } = "-";
    public string LastRawMessage { get; private set; } = "-";

    public event Action<string, string, string> MessageReceived;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;
    private readonly ConcurrentQueue<string> pendingMessages = new ConcurrentQueue<string>();
    private int queuedMessageCount;
    private int receivedThisSecond;
    private int droppedThisSecond;
    private int lastRateSecond;
    private readonly object rateLimitLock = new object();

    private void Start()
    {
        StartReceiver();
    }

    public int CurrentPort
    {
        get { return Mathf.Clamp(port, 1, 65535); }
    }

    private void Update()
    {
        int processed = 0;
        while (processed < maxMessagesPerFrame && pendingMessages.TryDequeue(out string rawMessage))
        {
            Interlocked.Decrement(ref queuedMessageCount);
            processed++;

            string message = rawMessage.Trim();
            string eventType = ExtractEventType(message);
            string payload = ExtractPayload(message);

            LastRawMessage = RuntimeLogSettings.MaskAndCompact(message);
            LastReceivedEventType = eventType;
            LastStatus = "Received: " + RuntimeLogSettings.MaskAndCompact(message);

            if (logReceivedMessages && RuntimeLogSettings.LogRawUdpMessages)
            {
                Debug.Log($"[UdpEventReceiver] Received raw='{RuntimeLogSettings.MaskAndCompact(message)}', eventType='{RuntimeLogSettings.MaskAndCompact(eventType)}', payload='{RuntimeLogSettings.MaskAndCompact(payload)}'");
            }

            MessageReceived?.Invoke(eventType, payload, message);
        }
    }

    private void StartReceiver()
    {
        if (running)
            return;

        try
        {
            udpClient = new UdpClient(port);
            running = true;
            LastStatus = $"Listening on {port}";
            LastReceivedEventType = "-";
            LastRawMessage = "-";

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log($"[UdpEventReceiver] UDP receiver started on port {port}");
        }
        catch (Exception ex)
        {
            LastStatus = $"Start failed: {ex.Message}";
            Debug.LogError($"[UdpEventReceiver] Failed to start UDP receiver: {SensitiveLogMasker.Mask(ex.ToString())}");
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                if (udpClient == null)
                    break;

                byte[] data = udpClient.Receive(ref remoteEndPoint);

                if (data == null || data.Length == 0)
                    continue;

                string message = Encoding.UTF8.GetString(data).Trim();

                if (!string.IsNullOrWhiteSpace(message) && ShouldAcceptIncomingMessage(data.Length))
                {
                    pendingMessages.Enqueue(message);
                    Interlocked.Increment(ref queuedMessageCount);
                }
            }
            catch (ThreadAbortException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (!running)
                    break;

                LastStatus = $"Socket error: {ex.Message}";
                Debug.LogWarning($"[UdpEventReceiver] UDP socket error while receiving: {SensitiveLogMasker.Mask(ex.ToString())}");
            }
            catch (ObjectDisposedException)
            {
                if (!running)
                    break;

                LastStatus = "Socket disposed";
            }
            catch (Exception ex)
            {
                if (!running)
                    break;

                LastStatus = $"Receive error: {ex.Message}";
                Debug.LogWarning($"[UdpEventReceiver] UDP receive error: {SensitiveLogMasker.Mask(ex.ToString())}");
            }
        }
    }

    private string ExtractEventType(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "-";

        int splitIndex = FindFirstSeparatorIndex(message);

        if (splitIndex <= 0)
            return message.Trim().ToLowerInvariant();

        return message.Substring(0, splitIndex).Trim().ToLowerInvariant();
    }

    private string ExtractPayload(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        int splitIndex = FindFirstSeparatorIndex(message);

        if (splitIndex < 0 || splitIndex >= message.Length - 1)
            return string.Empty;

        return message.Substring(splitIndex + 1).Trim().ToLowerInvariant();
    }

    private int FindFirstSeparatorIndex(string message)
    {
        int commaIndex = message.IndexOf(',');
        int colonIndex = message.IndexOf(':');
        int pipeIndex = message.IndexOf('|');

        int splitIndex = -1;

        if (commaIndex >= 0)
            splitIndex = commaIndex;

        if (colonIndex >= 0 && (splitIndex < 0 || colonIndex < splitIndex))
            splitIndex = colonIndex;

        if (pipeIndex >= 0 && (splitIndex < 0 || pipeIndex < splitIndex))
            splitIndex = pipeIndex;

        return splitIndex;
    }

    private void StopReceiver()
    {
        if (!running && udpClient == null && receiveThread == null)
            return;

        running = false;
        LastStatus = "Stopping...";

        try
        {
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UdpEventReceiver] Error while closing UDP client: {SensitiveLogMasker.Mask(ex.ToString())}");
        }

        try
        {
            if (receiveThread != null)
            {
                if (receiveThread.IsAlive)
                {
                    receiveThread.Join(1000);
                }

                receiveThread = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UdpEventReceiver] Error while stopping receive thread: {SensitiveLogMasker.Mask(ex.ToString())}");
        }

        LastStatus = "Stopped";
    }

    private bool ShouldAcceptIncomingMessage(int byteLength)
    {
        if (byteLength > maxMessageBytes)
        {
            NoteDroppedMessage("too large");
            return false;
        }

        lock (rateLimitLock)
        {
            int nowSecond = Environment.TickCount / 1000;
            if (nowSecond != lastRateSecond)
            {
                lastRateSecond = nowSecond;
                receivedThisSecond = 0;
                droppedThisSecond = 0;
            }

            if (receivedThisSecond >= maxMessagesPerSecond)
            {
                NoteDroppedMessageLocked("rate limited");
                return false;
            }

            if (queuedMessageCount >= maxQueuedMessages)
            {
                NoteDroppedMessageLocked("queue full");
                return false;
            }

            receivedThisSecond++;
            return true;
        }
    }

    private void NoteDroppedMessage(string reason)
    {
        lock (rateLimitLock)
        {
            NoteDroppedMessageLocked(reason);
        }
    }

    private void NoteDroppedMessageLocked(string reason)
    {
        droppedThisSecond++;
        if (droppedThisSecond == 1)
            LastStatus = "UDP dropped: " + reason;
    }

    public void RestartReceiver(int newPort)
    {
        port = Mathf.Clamp(newPort, 1, 65535);
        StopReceiver();
        StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    private void OnApplicationQuit()
    {
        StopReceiver();
    }
}
