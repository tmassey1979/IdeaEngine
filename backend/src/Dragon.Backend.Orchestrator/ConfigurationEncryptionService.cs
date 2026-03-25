using System.Security.Cryptography;
using System.Text;

namespace Dragon.Backend.Orchestrator;

public sealed class ConfigurationEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] key;

    public ConfigurationEncryptionService(string encodedKey)
    {
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            throw new InvalidOperationException("Missing DRAGON_CONFIG_ENCRYPTION_KEY for encrypted configuration access.");
        }

        try
        {
            key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("DRAGON_CONFIG_ENCRYPTION_KEY must be base64-encoded.", exception);
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException("DRAGON_CONFIG_ENCRYPTION_KEY must decode to exactly 32 bytes.");
        }
    }

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string encryptedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedValue);

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(encryptedValue);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Encrypted configuration value is not valid base64.", exception);
        }

        if (payload.Length <= NonceSize + TagSize)
        {
            throw new InvalidOperationException("Encrypted configuration payload is too short.");
        }

        var nonce = payload[..NonceSize];
        var tag = payload[NonceSize..(NonceSize + TagSize)];
        var ciphertext = payload[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Failed to decrypt the stored configuration.", exception);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    public static string GenerateEncodedKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
