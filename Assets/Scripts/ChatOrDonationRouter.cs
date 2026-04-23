using System;
using System.Collections.Generic;
using UnityEngine;

public class ChatOrDonationRouter : MonoBehaviour
{
    public enum ChatMatchMode
    {
        Exact,
        StartsWith,
        Contains
    }

    public enum AmountMatchMode
    {
        Exact,
        AtLeast,
        Range
    }

    [Serializable]
    public class ShakeSettings
    {
        public bool enabled = false;
        public float positionAmount = 0.02f;
        public float rotationAmount = 0.6f;
        public float duration = 0.08f;
    }

    [Serializable]
    public class ChatRule
    {
        public bool enabled = true;
        public string chatText = string.Empty;
        public ChatMatchMode matchMode = ChatMatchMode.Exact;
        public bool ignoreCase = true;
        public bool trimWhitespace = true;
        public string launchLabel = "Donation";
        public bool overrideEventData = false;
        public string spawnPattern = "single";
        public float power = 1.0f;
        public string direction = "forward";
        public int count = 1;
        public float scale = 1.0f;
        public string color = "";
        public int hitSoundIndex = 0;
        public bool projectileEnabled = true;
        public bool vmcEnabled = true;
        public float globalProjectilePowerMultiplier = 1.0f;
        public float globalVmcMultiplier = 1.0f;
        public float vmcImpulseX = 0f;
        public float vmcImpulseY = 0f;
        public float vmcImpulseZ = -0.02f;
        public float vmcYaw = 0f;
        public float vmcPitch = -1f;
        public float vmcRoll = 0f;
        public int vmcDurationMs = 120;
        public ShakeSettings screenShake = new ShakeSettings();
    }

    [Serializable]
    public class DonationRule
    {
        public bool enabled = true;
        public AmountMatchMode matchMode = AmountMatchMode.Exact;
        public int exactAmount = 1000;
        public int minimumAmount = 1000;
        public int rangeMin = 1000;
        public int rangeMax = 4999;
        public string launchLabel = "Donation";
        public bool overrideEventData = false;
        public string spawnPattern = "single";
        public float power = 1.0f;
        public string direction = "forward";
        public int count = 1;
        public float scale = 1.0f;
        public string color = "";
        public int hitSoundIndex = 0;
        public bool projectileEnabled = true;
        public bool vmcEnabled = true;
        public float globalProjectilePowerMultiplier = 1.0f;
        public float globalVmcMultiplier = 1.0f;
        public float vmcImpulseX = 0f;
        public float vmcImpulseY = 0f;
        public float vmcImpulseZ = -0.02f;
        public float vmcYaw = 0f;
        public float vmcPitch = -1f;
        public float vmcRoll = 0f;
        public int vmcDurationMs = 120;
        public ShakeSettings screenShake = new ShakeSettings();
    }

    [Serializable]
    public class CustomTriggerRule
    {
        public string chatText;
        public int donationAmount;
        public string launchLabel;
        public int hitSoundIndex;
    }

    [SerializeField] private SimpleLauncher simpleLauncher;
    [SerializeField] private List<ChatRule> chatRules = new List<ChatRule>();
    [SerializeField] private List<DonationRule> donationRules = new List<DonationRule>();
    [SerializeField] private bool logMatches = true;
    [SerializeField] private bool stopAfterFirstChatMatch = true;
    [SerializeField] private bool stopAfterFirstDonationMatch = true;
    [SerializeField] private KeyCode singleShotTestKey = KeyCode.F6;
    [SerializeField] private KeyCode shotgunTestKey = KeyCode.F7;
    [SerializeField] private KeyCode stickerTestKey = KeyCode.F8;

    void Awake()
    {
        if (simpleLauncher == null)
            simpleLauncher = UnityEngine.Object.FindAnyObjectByType<SimpleLauncher>();

        if (simpleLauncher == null)
            Debug.LogWarning("[ChatOrDonationRouter] SimpleLauncher를 찾지 못했습니다.");
    }

    void Update()
    {
        if (InputKeyHelper.GetKeyDown(singleShotTestKey))
            LaunchKeyboardTest("Donation", "single", 1, "forward");

        if (InputKeyHelper.GetKeyDown(shotgunTestKey))
            LaunchKeyboardTest("Spread", "burst", 3, "forward");

        if (InputKeyHelper.GetKeyDown(stickerTestKey))
            simpleLauncher?.LaunchByLabel("Sticker");
    }

    public void HandleChatMessage(string rawMessage, string senderId, string senderName)
    {
        if (simpleLauncher == null || string.IsNullOrWhiteSpace(rawMessage))
            return;

        bool matched = false;
        for (int i = 0; i < chatRules.Count; i++)
        {
            ChatRule rule = chatRules[i];
            if (rule == null || !rule.enabled || !IsChatMatched(rule, rawMessage))
                continue;

            matched = true;
            TestEventData eventData = rule.overrideEventData ? BuildEventDataFromChatRule(rule, rawMessage, senderName) : null;

            if (IsStickerBoxEvent(rule.launchLabel, eventData))
                continue;

            if (logMatches && RuntimeLogSettings.MatchedEventLogs)
                Debug.Log($"[ChatOrDonationRouter] Chat matched: \"{RuntimeLogSettings.MaskAndCompact(rawMessage)}\" -> {rule.launchLabel}");

            simpleLauncher.LaunchByLabel(ResolveLaunchRequestLabel(rule.launchLabel, eventData), eventData);
            if (stopAfterFirstChatMatch)
                return;
        }

        if (!matched && logMatches && RuntimeLogSettings.LogRuleMisses)
            Debug.Log($"[ChatOrDonationRouter] No chat rule matched: \"{RuntimeLogSettings.MaskAndCompact(rawMessage)}\"");
    }

    public void HandleDonation(int amount, string senderId, string senderName, string message)
    {
        if (simpleLauncher == null)
            return;

        bool matched = false;
        for (int i = donationRules.Count - 1; i >= 0; i--)
        {
            DonationRule rule = donationRules[i];
            if (rule == null || !rule.enabled || !IsDonationMatched(rule, amount))
                continue;

            matched = true;
            TestEventData eventData = rule.overrideEventData ? BuildEventDataFromDonationRule(rule, amount, senderName, message) : null;

            if (IsStickerBoxEvent(rule.launchLabel, eventData))
                continue;

            if (logMatches && RuntimeLogSettings.MatchedEventLogs)
                Debug.Log($"[ChatOrDonationRouter] Donation matched: {amount} -> {rule.launchLabel}");

            simpleLauncher.LaunchByLabel(ResolveLaunchRequestLabel(rule.launchLabel, eventData), eventData);
            if (stopAfterFirstDonationMatch)
                return;
        }

        if (!matched && logMatches && RuntimeLogSettings.LogRuleMisses)
            Debug.Log($"[ChatOrDonationRouter] No donation rule matched: {amount}");
    }

    public void ReplaceCustomRules(List<CustomTriggerRule> rules)
    {
        chatRules.Clear();
        donationRules.Clear();

        if (rules == null)
            return;

        Dictionary<string, ChatRule> uniqueChatRules = new Dictionary<string, ChatRule>(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, DonationRule> uniqueDonationRules = new Dictionary<int, DonationRule>();

        for (int i = 0; i < rules.Count; i++)
        {
            CustomTriggerRule rule = rules[i];
            if (rule == null)
                continue;

            string configLabel = ResolveDefaultConfigLabel(rule.launchLabel);
            if (string.IsNullOrWhiteSpace(configLabel))
                continue;

            string chatText = string.IsNullOrWhiteSpace(rule.chatText) ? string.Empty : rule.chatText.Trim();
            if (!string.IsNullOrWhiteSpace(chatText) && !string.Equals(chatText, "채팅 없음", StringComparison.OrdinalIgnoreCase))
            {
                uniqueChatRules[chatText] = new ChatRule
                {
                    enabled = true,
                    chatText = chatText,
                    matchMode = ChatMatchMode.Contains,
                    ignoreCase = true,
                    trimWhitespace = true,
                    launchLabel = configLabel,
                    overrideEventData = true,
                    spawnPattern = ResolveDefaultSpawnPattern(configLabel),
                    count = ResolveDefaultCount(configLabel),
                    direction = ResolveDefaultDirection(configLabel),
                    hitSoundIndex = rule.hitSoundIndex
                };
            }

            if (rule.donationAmount > 0)
            {
                uniqueDonationRules[rule.donationAmount] = new DonationRule
                {
                    enabled = true,
                    matchMode = AmountMatchMode.Exact,
                    exactAmount = rule.donationAmount,
                    launchLabel = configLabel,
                    overrideEventData = true,
                    spawnPattern = ResolveDefaultSpawnPattern(configLabel),
                    count = ResolveDefaultCount(configLabel),
                    direction = ResolveDefaultDirection(configLabel),
                    hitSoundIndex = rule.hitSoundIndex
                };
            }
        }

        foreach (ChatRule rule in uniqueChatRules.Values)
            chatRules.Add(rule);

        foreach (DonationRule rule in uniqueDonationRules.Values)
            donationRules.Add(rule);
    }

    public void LaunchDirect(string launchLabel)
    {
        if (simpleLauncher == null)
            return;

        simpleLauncher.LaunchByLabel(ResolveDefaultConfigLabel(launchLabel));
    }

    private void LaunchKeyboardTest(string launchLabel, string spawnPattern, int count, string direction)
    {
        if (simpleLauncher == null)
            return;

        simpleLauncher.LaunchByLabel(
            ResolveDefaultConfigLabel(launchLabel),
            new TestEventData
            {
                eventType = launchLabel,
                nickname = "KeyboardTest",
                message = launchLabel,
                amount = 0,
                spawnPattern = spawnPattern,
                power = 1f,
                direction = direction,
                count = Mathf.Max(1, count),
                scale = 1f,
                color = string.Empty,
                projectileEnabled = true,
                vmcEnabled = true,
                cameraRecoilEnabled = false,
                globalProjectilePowerMultiplier = 1f,
                globalVmcMultiplier = 1f,
                vmcImpulseX = 0f,
                vmcImpulseY = 0f,
                vmcImpulseZ = 0f,
                vmcYaw = 0f,
                vmcPitch = 0f,
                vmcRoll = 0f,
                vmcDurationMs = 120
            });
    }

    private bool IsChatMatched(ChatRule rule, string rawMessage)
    {
        string input = rawMessage ?? string.Empty;
        string target = rule.chatText ?? string.Empty;

        if (rule.trimWhitespace)
        {
            input = input.Trim();
            target = target.Trim();
        }

        if (rule.ignoreCase)
        {
            input = input.ToLowerInvariant();
            target = target.ToLowerInvariant();
        }

        if (string.IsNullOrEmpty(target))
            return false;

        switch (rule.matchMode)
        {
            case ChatMatchMode.StartsWith:
                return input.StartsWith(target);
            case ChatMatchMode.Contains:
                return input.Contains(target);
            default:
                return input == target;
        }
    }

    private bool IsDonationMatched(DonationRule rule, int amount)
    {
        switch (rule.matchMode)
        {
            case AmountMatchMode.AtLeast:
                return amount >= rule.minimumAmount;
            case AmountMatchMode.Range:
                return amount >= rule.rangeMin && amount <= rule.rangeMax;
            default:
                return amount == rule.exactAmount;
        }
    }

    private TestEventData BuildEventDataFromChatRule(ChatRule rule, string rawMessage, string senderName)
    {
        return new TestEventData
        {
            eventType = rule.launchLabel,
            nickname = string.IsNullOrWhiteSpace(senderName) ? "ChatUser" : senderName,
            message = rawMessage,
            amount = 0,
            spawnPattern = rule.spawnPattern,
            power = rule.power,
            direction = rule.direction,
            count = Mathf.Max(1, rule.count),
            scale = Mathf.Max(0.01f, rule.scale),
            color = rule.color,
            hitSoundIndex = rule.hitSoundIndex,
            projectileEnabled = rule.projectileEnabled,
            vmcEnabled = rule.vmcEnabled,
            globalProjectilePowerMultiplier = SanitizeMultiplier(rule.globalProjectilePowerMultiplier),
            globalVmcMultiplier = SanitizeMultiplier(rule.globalVmcMultiplier),
            vmcImpulseX = rule.vmcImpulseX,
            vmcImpulseY = rule.vmcImpulseY,
            vmcImpulseZ = rule.vmcImpulseZ,
            vmcYaw = rule.vmcYaw,
            vmcPitch = rule.vmcPitch,
            vmcRoll = rule.vmcRoll,
            vmcDurationMs = rule.vmcDurationMs
        };
    }

    private TestEventData BuildEventDataFromDonationRule(DonationRule rule, int amount, string senderName, string message)
    {
        return new TestEventData
        {
            eventType = rule.launchLabel,
            nickname = string.IsNullOrWhiteSpace(senderName) ? "Donator" : senderName,
            message = message,
            amount = amount,
            spawnPattern = rule.spawnPattern,
            power = rule.power,
            direction = rule.direction,
            count = Mathf.Max(1, rule.count),
            scale = Mathf.Max(0.01f, rule.scale),
            color = rule.color,
            hitSoundIndex = rule.hitSoundIndex,
            projectileEnabled = rule.projectileEnabled,
            vmcEnabled = rule.vmcEnabled,
            globalProjectilePowerMultiplier = SanitizeMultiplier(rule.globalProjectilePowerMultiplier),
            globalVmcMultiplier = SanitizeMultiplier(rule.globalVmcMultiplier),
            vmcImpulseX = rule.vmcImpulseX,
            vmcImpulseY = rule.vmcImpulseY,
            vmcImpulseZ = rule.vmcImpulseZ,
            vmcYaw = rule.vmcYaw,
            vmcPitch = rule.vmcPitch,
            vmcRoll = rule.vmcRoll,
            vmcDurationMs = rule.vmcDurationMs
        };
    }

    private float SanitizeMultiplier(float value)
    {
        return value <= 0f ? 1f : value;
    }

    private string ResolveDefaultConfigLabel(string launchLabel)
    {
        string normalized = NormalizeLaunchLabel(launchLabel);
        switch (normalized)
        {
            case "single":
                return "Donation";
            case "shotgun":
                return "Spread";
            case "machinegun":
                return "Machine Gun";
            case "sticker":
                return "Sticker";
            case "bigdonation":
                return "Big Donation";
            default:
                return launchLabel;
        }
    }

    private string ResolveDefaultSpawnPattern(string launchLabel)
    {
        string normalized = NormalizeLaunchLabel(launchLabel);
        if (normalized == "shotgun")
            return "burst";
        return "single";
    }

    private int ResolveDefaultCount(string launchLabel)
    {
        return NormalizeLaunchLabel(launchLabel) == "shotgun" ? 3 : 1;
    }

    private string ResolveDefaultDirection(string launchLabel)
    {
        return "forward";
    }

    private string ResolveLaunchRequestLabel(string launchLabel, TestEventData eventData)
    {
        string normalizedLabel = NormalizeLaunchLabel(launchLabel);
        if (normalizedLabel == "machinegun")
            return "Machine Gun";
        if (normalizedLabel == "sticker")
            return "Sticker";
        if (normalizedLabel == "box")
            return string.Empty;

        if (eventData != null)
        {
            string pattern = string.IsNullOrWhiteSpace(eventData.spawnPattern) ? "single" : eventData.spawnPattern.Trim().ToLowerInvariant();
            if (pattern == "burst")
                return "Spread";
            if (pattern == "rain")
                return string.Empty;
            return "Donation";
        }

        return ResolveDefaultConfigLabel(launchLabel);
    }

    private string NormalizeLaunchLabel(string launchLabel)
    {
        string value = string.IsNullOrWhiteSpace(launchLabel) ? string.Empty : launchLabel.Trim().ToLowerInvariant();
        switch (value)
        {
            case "single":
            case "donation":
            case "싱글샷":
                return "single";
            case "shotgun":
            case "spread":
            case "샷건":
                return "shotgun";
            case "box":
            case "rain":
            case "박스":
                return "box";
            case "machine gun":
            case "machinegun":
            case "\uAE30\uAD00\uCD1D":
                return "machinegun";
            case "sticker":
            case "\uC2A4\uD2F0\uCEE4 \uBD99\uC774\uAE30":
                return "sticker";
            case "big donation":
                return "bigdonation";
            default:
                return value;
        }
    }

    private bool IsStickerBoxEvent(string launchLabel, TestEventData eventData)
    {
        if (NormalizeLaunchLabel(launchLabel) == "box")
            return true;

        return eventData != null &&
            !string.IsNullOrWhiteSpace(eventData.spawnPattern) &&
            eventData.spawnPattern.Trim().Equals("rain", StringComparison.OrdinalIgnoreCase);
    }
}
