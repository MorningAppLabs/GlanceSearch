using System;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace GlanceSearch.Infrastructure.Settings;

/// <summary>
/// Helper for encrypting and decrypting sensitive strings using Windows DPAPI.
/// </summary>
public static class SecurityHelper
{
    // Entropy to add a bit more obfuscation, though DPAPI already uses the user profile.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GlanceSearch_SecureStore");

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to encrypt data using DPAPI.");
            return string.Empty;
        }
    }

    public static string Decrypt(string encryptedBase64Text)
    {
        if (string.IsNullOrEmpty(encryptedBase64Text))
            return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64Text);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            // If it's not base64, it might be an old unencrypted key from before the update
            return encryptedBase64Text;
        }
        catch (CryptographicException)
        {
            // If it fails to decrypt, it might be an old unencrypted key
            return encryptedBase64Text;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to decrypt data using DPAPI.");
            return string.Empty;
        }
    }
}
