using System;

[Serializable]
public class AuthApiResponseBase
{
    public bool ok;
}

[Serializable]
public class AuthStartRequest
{
    public string installId;
}

[Serializable]
public class AuthStartResponse : AuthApiResponseBase
{
    public string authUrl;
    public string loginTicket;
    public int expiresIn;
}

[Serializable]
public class AuthFinishRequest
{
    public string installId;
    public string loginTicket;
    public string code;
    public string state;
}

[Serializable]
public class AuthFinishResponse : AuthApiResponseBase
{
    public string appSessionToken;
    public string accessToken;
    public int expiresIn;
    public string channelId;
}

[Serializable]
public class SessionAccessTokenRequest
{
    public string appSessionToken;
}

[Serializable]
public class SessionAccessTokenResponse : AuthApiResponseBase
{
    public string accessToken;
    public int expiresIn;
}

[Serializable]
public class SessionLogoutRequest
{
    public string appSessionToken;
}

[Serializable]
public class SessionLogoutResponse : AuthApiResponseBase
{
}

[Serializable]
public class StoredAuthSession
{
    public string installId;
    public string backendBaseUrl;
    public string appSessionToken;
    public string accessToken;
    public long accessTokenExpiresAtUnix;
    public string channelId;
}
