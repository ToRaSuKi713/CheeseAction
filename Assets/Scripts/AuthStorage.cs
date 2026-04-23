using System;
using System.Text;
using UnityEngine;

public enum AuthStorageFailureKind
{
    TokenSaveFailed,
    TokenDecryptFailed
}

public sealed class AuthStorageException : Exception
{
    public AuthStorageFailureKind FailureKind { get; }

    public AuthStorageException(AuthStorageFailureKind failureKind, string message, Exception innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }
}

public static class AuthStorage
{
    public const string InstallIdKey = "auth.installId";
    public const string BackendBaseUrlKey = "auth.backendBaseUrl";
    public const string AppSessionTokenKey = "auth.appSessionToken";
    public const string ChannelIdKey = "auth.channelId";
    public const string LegacyChzzkChannelKey = "ui.chzzk.channel";
    public const string LegacyChzzkTokenKey = "ui.chzzk.token";
    public const string LegacyAccessTokenKey = "auth.accessToken";
    public const string LegacyRefreshTokenKey = "auth.refreshToken";

    public static string GetOrCreateInstallId()
    {
        string installId = PlayerPrefs.GetString(InstallIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(installId))
            return installId;

        installId = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(InstallIdKey, installId);
        PlayerPrefs.Save();
        return installId;
    }

    public static string GetBackendBaseUrl(string fallback)
    {
        return PlayerPrefs.GetString(BackendBaseUrlKey, fallback ?? string.Empty);
    }

    public static void SetBackendBaseUrl(string value)
    {
        PlayerPrefs.SetString(BackendBaseUrlKey, string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim());
        PlayerPrefs.Save();
    }

    public static StoredAuthSession LoadSession()
    {
        return new StoredAuthSession
        {
            installId = PlayerPrefs.GetString(InstallIdKey, string.Empty),
            backendBaseUrl = PlayerPrefs.GetString(BackendBaseUrlKey, string.Empty),
            appSessionToken = LoadProtectedString(AppSessionTokenKey),
            accessToken = string.Empty,
            accessTokenExpiresAtUnix = 0,
            channelId = PlayerPrefs.GetString(ChannelIdKey, string.Empty)
        };
    }

    public static void SaveSession(string appSessionToken, string channelId)
    {
        SaveProtectedString(AppSessionTokenKey, appSessionToken);
        PlayerPrefs.SetString(ChannelIdKey, channelId ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void SaveAccessToken(string accessToken, DateTimeOffset expiresAtUtc)
    {
    }

    public static void ClearSession()
    {
        PlayerPrefs.DeleteKey(AppSessionTokenKey);
        PlayerPrefs.DeleteKey(ChannelIdKey);
        PlayerPrefs.DeleteKey(LegacyAccessTokenKey);
        PlayerPrefs.DeleteKey(LegacyRefreshTokenKey);
        PlayerPrefs.Save();
    }

    public static void ClearLegacySensitivePrefs()
    {
        bool dirty = false;

        if (PlayerPrefs.HasKey(LegacyChzzkChannelKey))
        {
            PlayerPrefs.DeleteKey(LegacyChzzkChannelKey);
            dirty = true;
        }

        if (PlayerPrefs.HasKey(LegacyChzzkTokenKey))
        {
            PlayerPrefs.DeleteKey(LegacyChzzkTokenKey);
            dirty = true;
        }

        if (PlayerPrefs.HasKey(LegacyAccessTokenKey))
        {
            PlayerPrefs.DeleteKey(LegacyAccessTokenKey);
            dirty = true;
        }

        if (PlayerPrefs.HasKey(LegacyRefreshTokenKey))
        {
            PlayerPrefs.DeleteKey(LegacyRefreshTokenKey);
            dirty = true;
        }

        if (dirty)
            PlayerPrefs.Save();
    }

    private static void SaveProtectedString(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            PlayerPrefs.DeleteKey(key);
            return;
        }

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(value);
            byte[] entropyBytes = Encoding.UTF8.GetBytes(Application.companyName + "|" + Application.productName + "|" + key);
            byte[] protectedBytes = WindowsDpapi.Protect(plainBytes, entropyBytes);
            PlayerPrefs.SetString(key, Convert.ToBase64String(protectedBytes));
        }
        catch (Exception ex)
        {
            throw new AuthStorageException(AuthStorageFailureKind.TokenSaveFailed, "appSessionToken 저장 실패", ex);
        }
    }

    private static string LoadProtectedString(string key)
    {
        string stored = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        try
        {
            byte[] protectedBytes = Convert.FromBase64String(stored);
            byte[] entropyBytes = Encoding.UTF8.GetBytes(Application.companyName + "|" + Application.productName + "|" + key);
            byte[] plainBytes = WindowsDpapi.Unprotect(protectedBytes, entropyBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            throw new AuthStorageException(AuthStorageFailureKind.TokenDecryptFailed, "appSessionToken 복호화 실패", ex);
        }
    }
}
