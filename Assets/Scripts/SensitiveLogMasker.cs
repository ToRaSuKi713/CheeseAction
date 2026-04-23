using System.Text.RegularExpressions;

public static class SensitiveLogMasker
{
    private static readonly string[] SensitiveKeys =
    {
        "code",
        "state",
        "accessToken",
        "refreshToken",
        "appSessionToken",
        "auth",
        "sessionKey",
        "channelId",
        "senderChannelId",
        "donatorChannelId",
        "subscriberChannelId",
        "senderId",
        "userId",
        "memberId",
        "installId",
        "loginTicket",
        "nickname",
        "donatorNickname",
        "subscriberNickname",
        "senderName",
        "userName",
        "name"
    };

    public static string Mask(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string result = text;
        for (int i = 0; i < SensitiveKeys.Length; i++)
            result = MaskJsonOrQueryValue(result, SensitiveKeys[i]);

        return result;
    }

    private static string MaskJsonOrQueryValue(string text, string key)
    {
        string masked = Regex.Replace(
            text,
            $"(\"{Regex.Escape(key)}\"\\s*:\\s*\")([^\"]+)(\")",
            $"$1***$3",
            RegexOptions.IgnoreCase);

        masked = Regex.Replace(
            masked,
            $"({Regex.Escape(key)}=)([^&\\s]+)",
            "$1***",
            RegexOptions.IgnoreCase);

        return masked;
    }
}
