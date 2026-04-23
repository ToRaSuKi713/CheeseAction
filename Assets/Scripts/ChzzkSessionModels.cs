using System;

[Serializable]
public class ChzzkSessionAuthResponse
{
    public string url;
}

[Serializable]
public class ChzzkSystemMessage
{
    public string type;
    public ChzzkSystemData data;
}

[Serializable]
public class ChzzkSystemData
{
    public string sessionKey;
    public string eventType;
    public string channelId;
}

[Serializable]
public class ChzzkChatProfile
{
    public string nickname;
}

[Serializable]
public class ChzzkChatMessage
{
    public string channelId;
    public string senderChannelId;
    public ChzzkChatProfile profile;
    public string content;
    public long messageTime;
}

[Serializable]
public class ChzzkDonationMessage
{
    public string donationType;
    public string channelId;
    public string donatorChannelId;
    public string donatorNickname;
    public string payAmount;
    public string donationText;
}
