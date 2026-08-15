using System.Security.Cryptography;
using System.Text;
using Playnite;

namespace PlayStationLibrary;

/// <summary>
/// Protects credentials at rest with DPAPI so that another process running as a different user, or
/// a script reading the plugin's data directory, cannot lift them straight out of a file.
/// </summary>
internal static class PlayStationSecrets
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private static readonly byte[] entropy = Encoding.UTF8.GetBytes("PlayStationLibrary.Secrets.v1");

    public static string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        try
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to protect a PlayStation credential.");
            return null;
        }
    }

    public static string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (TryUnprotect(value, out var unprotected))
        {
            return unprotected;
        }

        logger.Error("Failed to read a stored PlayStation credential. It has to be entered again.");
        return null;
    }

    /// <summary>
    /// Reads an encrypted credential, preserving the plain-text NPSSO format written by the first
    /// Playnite 11 builds. The caller writes it back through <see cref="Protect"/> on the next
    /// settings save, completing the one-time migration.
    /// </summary>
    public static string UnprotectOrMigrate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return TryUnprotect(value, out var unprotected) ? unprotected! : value;
    }

    private static bool TryUnprotect(string value, out string? result)
    {
        result = null;
        try
        {
            result = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), entropy, DataProtectionScope.CurrentUser));
            return true;
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
