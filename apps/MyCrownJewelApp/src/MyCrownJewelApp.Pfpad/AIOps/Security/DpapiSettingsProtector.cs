using System.Security.Cryptography;
using System.Text;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Protects sensitive AIOps configuration values using Windows DPAPI
/// (Data Protection API) with <see cref="DataProtectionScope.CurrentUser"/>.
/// Encrypted values are bound to the current Windows user account on this
/// machine and cannot be decrypted by any other user or on any other machine.
/// </summary>
/// <remarks>
/// Encrypted output is base64-encoded and safe to store in plain-text settings
/// files. The application-specific entropy string ensures that blobs produced
/// by pfpad cannot be decrypted by unrelated applications that also use DPAPI.
/// </remarks>
public static class DpapiSettingsProtector
{
    // Application-specific entropy — ties encrypted blobs to pfpad AIOps v1.
    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("pfpad-aiops-settings-v1");

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with DPAPI (CurrentUser scope)
    /// and returns a base64-encoded ciphertext string.
    /// Returns <see cref="string.Empty"/> when the input is null or empty.
    /// </summary>
    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        try
        {
            byte[] data   = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipher = ProtectedData.Protect(data, s_entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipher);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DPAPI] Protect failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Decrypts a DPAPI-protected base64 string back to plaintext.
    /// Returns <see cref="string.Empty"/> when the input is null/empty or
    /// decryption fails (e.g. different machine, different Windows user, or
    /// corrupted ciphertext).
    /// </summary>
    public static string Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return string.Empty;

        try
        {
            byte[] cipher = Convert.FromBase64String(ciphertext);
            byte[] data   = ProtectedData.Unprotect(cipher, s_entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DPAPI] Unprotect failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> looks like a DPAPI
    /// base64 blob (non-empty, valid base64, at least 44 chars).
    /// This is a heuristic — not a cryptographic guarantee.
    /// </summary>
    public static bool IsProtected(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 44)
            return false;

        try
        {
            _ = Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
