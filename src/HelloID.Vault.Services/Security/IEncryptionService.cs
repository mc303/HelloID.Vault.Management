namespace HelloID.Vault.Services.Security;

/// <summary>
/// Service interface for encrypting and decrypting sensitive data.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plain text data.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <returns>Base64-encoded encrypted data.</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts encrypted data.
    /// </summary>
    /// <param name="cipherText">Base64-encoded encrypted data, or null to return null.</param>
    /// <returns>The decrypted plain text, or null if input was null or empty.</returns>
    string? Decrypt(string? cipherText);
}
