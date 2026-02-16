using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace HelloID.Vault.Services.Security;

/// <summary>
/// Windows DPAPI (Data Protection API) based encryption service.
/// Uses the ProtectedData class to encrypt/decrypt data tied to the current user account.
/// </summary>
/// <remarks>
/// Security notes:
/// - Uses DataProtectionScope.CurrentUser (User Scope) - encryption is tied to the current Windows user account
/// - Uses additional entropy (salting) to make it harder for attackers to decrypt data even with access to the user account
/// - Entropy is application-specific and prevents other applications from decrypting the data
/// </remarks>
public class WindowsDpapiEncryptionService : IEncryptionService
{
    /// <summary>
    /// Additional entropy (salt) for DPAPI encryption.
    /// This makes it harder for attackers to decrypt data even with access to the user account.
    /// The entropy must be the same for both encryption and decryption.
    /// </summary>
    private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("HelloID.Vault.Management-2026-Supabase");

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentException("Plain text cannot be null or empty.", nameof(plainText));
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EncryptionService] Encryption failed: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return null;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(cipherText);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EncryptionService] Decryption failed: {ex.Message}");
            return null;
        }
    }
}
