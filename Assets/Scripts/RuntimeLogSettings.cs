public static class RuntimeLogSettings
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool VerboseRealtimeLogs = true;
#else
    public static bool VerboseRealtimeLogs = false;
#endif

    public static bool MatchedEventLogs = true;
    public static int MaxStatusChars = 180;

    public static bool LogRawUdpMessages
    {
        get { return VerboseRealtimeLogs; }
    }

    public static bool LogSocketPayloads
    {
        get { return VerboseRealtimeLogs; }
    }

    public static bool LogRealtimeChatMessages
    {
        get { return VerboseRealtimeLogs; }
    }

    public static bool LogRuleMisses
    {
        get { return VerboseRealtimeLogs; }
    }

    public static string MaskAndCompact(string text)
    {
        return Compact(SensitiveLogMasker.Mask(text));
    }

    public static string Compact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        int max = MaxStatusChars < 32 ? 32 : MaxStatusChars;
        return text.Length <= max ? text : text.Substring(0, max) + "...";
    }
}
