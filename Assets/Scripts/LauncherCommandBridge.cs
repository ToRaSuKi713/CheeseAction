using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

public class LauncherCommandBridge : MonoBehaviour
{
    [SerializeField] private ChatOrDonationRouter router;
    [SerializeField] private UdpEventReceiver udpReceiver;

    void Awake()
    {
        if (router == null)
            router = Object.FindAnyObjectByType<ChatOrDonationRouter>();

        if (udpReceiver == null)
            udpReceiver = Object.FindAnyObjectByType<UdpEventReceiver>();

        if (router == null)
            Debug.LogWarning("[LauncherCommandBridge] ChatOrDonationRouter를 찾지 못했습니다.");
    }

    void OnEnable()
    {
        SubscribeReceiver();
    }

    void Start()
    {
        SubscribeReceiver();
    }

    void OnDisable()
    {
        UnsubscribeReceiver();
    }

    public void HandleMessageReceived(string rawMessage, string senderId, string senderName)
    {
        if (router == null || string.IsNullOrWhiteSpace(rawMessage))
            return;

        if (TryExtractDonationAmount(rawMessage, out int amount))
        {
            HandleDonationEvent(amount, rawMessage, senderId, senderName);
            return;
        }

        HandleChatEvent(rawMessage, senderId, senderName);
    }

    public void HandleChatEvent(string message, string senderId, string senderName)
    {
        if (router == null || string.IsNullOrWhiteSpace(message))
            return;

        router.HandleChatMessage(message, senderId, senderName);
    }

    public void HandleDonationEvent(int amount, string message, string senderId, string senderName)
    {
        if (router == null || amount <= 0)
            return;

        router.HandleDonation(amount, senderId, senderName, message);
    }

    private void SubscribeReceiver()
    {
        if (udpReceiver == null)
            udpReceiver = Object.FindAnyObjectByType<UdpEventReceiver>();

        if (udpReceiver == null)
            return;

        udpReceiver.MessageReceived -= HandleUdpMessageReceived;
        udpReceiver.MessageReceived += HandleUdpMessageReceived;
    }

    private void UnsubscribeReceiver()
    {
        if (udpReceiver == null)
            return;

        udpReceiver.MessageReceived -= HandleUdpMessageReceived;
    }

    private void HandleUdpMessageReceived(string eventType, string payload, string rawMessage)
    {
        if (router == null)
            router = Object.FindAnyObjectByType<ChatOrDonationRouter>();

        if (router == null)
            return;

        if (TryHandleBridgePayload(rawMessage))
            return;

        string normalizedType = Normalize(eventType);
        string normalizedPayload = string.IsNullOrWhiteSpace(payload) ? string.Empty : payload.Trim();
        string normalizedRaw = string.IsNullOrWhiteSpace(rawMessage) ? string.Empty : rawMessage.Trim();
        string senderId = ExtractSenderId(normalizedRaw);
        string senderName = ExtractSenderName(normalizedRaw);

        if (IsDonationEvent(normalizedType, normalizedPayload, normalizedRaw))
        {
            if (TryExtractDonationAmount(normalizedPayload, out int payloadAmount) ||
                TryExtractDonationAmount(normalizedRaw, out payloadAmount))
            {
                HandleDonationEvent(payloadAmount, normalizedRaw, senderId, senderName);
                return;
            }
        }

        string chatText = ExtractChatMessage(normalizedType, normalizedPayload, normalizedRaw);
        if (!string.IsNullOrWhiteSpace(chatText))
            HandleChatEvent(chatText, senderId, senderName);
    }

    private bool TryHandleBridgePayload(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            return false;

        string text = rawMessage.Trim();
        if (!text.StartsWith("{") || !text.EndsWith("}"))
            return false;

        BridgeEventPayload payload = null;
        try
        {
            payload = JsonUtility.FromJson<BridgeEventPayload>(text);
        }
        catch
        {
            payload = null;
        }

        if (payload == null)
            return false;

        string eventType = Normalize(payload.eventType);
        string senderName = string.IsNullOrWhiteSpace(payload.nickname) ? ExtractSenderName(text) : payload.nickname;
        string senderId = ExtractSenderId(text);
        string message = string.IsNullOrWhiteSpace(payload.message) ? string.Empty : payload.message.Trim();

        if (payload.amount > 0 || eventType == "donation" || eventType == "support" || eventType == "cheer" || eventType == "후원" || eventType == "도네")
        {
            HandleDonationEvent(payload.amount, message, senderId, senderName);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            HandleChatEvent(message, senderId, senderName);
            return true;
        }

        return false;
    }

    private string ExtractChatMessage(string eventType, string payload, string rawMessage)
    {
        if (TryExtractJsonString(rawMessage, "message", out string value) ||
            TryExtractJsonString(rawMessage, "chat", out value) ||
            TryExtractJsonString(rawMessage, "text", out value) ||
            TryExtractJsonString(rawMessage, "content", out value))
            return value;

        if (eventType == "chat" || eventType == "message" || eventType == "comment")
            return string.IsNullOrWhiteSpace(payload) ? rawMessage : payload;

        if (!IsDonationEvent(eventType, payload, rawMessage))
            return string.IsNullOrWhiteSpace(payload) ? rawMessage : payload;

        return string.Empty;
    }

    private bool IsDonationEvent(string eventType, string payload, string rawMessage)
    {
        if (eventType == "donation" || eventType == "donate" || eventType == "support" || eventType == "cheer")
            return true;

        string raw = Normalize(rawMessage);
        string body = Normalize(payload);
        return raw.Contains("\"amount\"") ||
               raw.Contains("\"donation\"") ||
               raw.Contains("\"support\"") ||
               raw.Contains("후원") ||
               raw.Contains("도네") ||
               body.Contains("후원") ||
               body.Contains("도네");
    }

    private string ExtractSenderId(string rawMessage)
    {
        if (TryExtractJsonString(rawMessage, "senderId", out string value) ||
            TryExtractJsonString(rawMessage, "userId", out value) ||
            TryExtractJsonString(rawMessage, "memberId", out value))
            return value;

        return string.Empty;
    }

    private string ExtractSenderName(string rawMessage)
    {
        if (TryExtractJsonString(rawMessage, "senderName", out string value) ||
            TryExtractJsonString(rawMessage, "nickname", out value) ||
            TryExtractJsonString(rawMessage, "userName", out value) ||
            TryExtractJsonString(rawMessage, "name", out value))
            return value;

        return string.Empty;
    }

    private bool TryExtractDonationAmount(string rawMessage, out int amount)
    {
        amount = 0;
        string text = string.IsNullOrWhiteSpace(rawMessage) ? string.Empty : rawMessage.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (TryExtractJsonInt(text, "amount", out amount) ||
            TryExtractJsonInt(text, "donation", out amount) ||
            TryExtractJsonInt(text, "price", out amount))
            return amount > 0;

        if (int.TryParse(text, out amount) && amount > 0)
            return true;

        string lower = Normalize(text);
        bool donationHint =
            lower.Contains("donation") ||
            lower.Contains("amount") ||
            lower.Contains("support") ||
            lower.Contains("후원") ||
            lower.Contains("도네") ||
            lower.Contains("원");

        string digits = string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsDigit(c))
                digits += c;
        }

        if (!string.IsNullOrWhiteSpace(digits) && int.TryParse(digits, out amount) && amount > 0)
            return donationHint;

        return false;
    }

    private bool TryExtractJsonString(string raw, string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(key))
            return false;

        Match match = Regex.Match(raw, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        value = match.Groups[1].Value.Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private bool TryExtractJsonInt(string raw, string key, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(key))
            return false;

        Match match = Regex.Match(raw, $"\"{Regex.Escape(key)}\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        return int.TryParse(match.Groups[1].Value, out value);
    }

    private string Normalize(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().ToLowerInvariant();
    }
}
