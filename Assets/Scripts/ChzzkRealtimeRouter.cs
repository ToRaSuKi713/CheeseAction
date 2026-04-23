using UnityEngine;
using Object = UnityEngine.Object;

public class ChzzkRealtimeRouter : MonoBehaviour
{
    [SerializeField] private LauncherCommandBridge launcherCommandBridge;

    void Awake()
    {
        if (launcherCommandBridge == null)
            launcherCommandBridge = Object.FindAnyObjectByType<LauncherCommandBridge>();
    }

    public void RouteChat(ChzzkChatMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.content))
            return;

        if (launcherCommandBridge == null)
            launcherCommandBridge = Object.FindAnyObjectByType<LauncherCommandBridge>();

        string nickname = message.profile != null ? message.profile.nickname : string.Empty;
        string content = message.content.Trim();
        if (RuntimeLogSettings.LogRealtimeChatMessages)
            Debug.Log($"[CHZZK CHAT] {RuntimeLogSettings.MaskAndCompact(nickname)}: {RuntimeLogSettings.MaskAndCompact(content)}");

        launcherCommandBridge?.HandleChatEvent(content, message.senderChannelId, nickname);
    }

    public void RouteDonation(ChzzkDonationMessage message)
    {
        if (message == null)
            return;

        if (launcherCommandBridge == null)
            launcherCommandBridge = Object.FindAnyObjectByType<LauncherCommandBridge>();

        int amount = ParseAmount(message.payAmount);
        string donationText = string.IsNullOrWhiteSpace(message.donationText) ? string.Empty : message.donationText.Trim();

        if (RuntimeLogSettings.LogRealtimeChatMessages)
            Debug.Log($"[CHZZK DONATION] {RuntimeLogSettings.MaskAndCompact(message.donatorNickname)} ({amount}): {RuntimeLogSettings.MaskAndCompact(donationText)}");

        launcherCommandBridge?.HandleDonationEvent(amount, donationText, message.donatorChannelId, message.donatorNickname);
    }

    private int ParseAmount(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        string digits = string.Empty;
        for (int i = 0; i < raw.Length; i++)
        {
            if (char.IsDigit(raw[i]))
                digits += raw[i];
        }

        return int.TryParse(digits, out int value) ? value : 0;
    }
}
