using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace IRS.LLM.Services;

public class EncryptionService : IEncryptionService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private readonly string? _configIv;

    public EncryptionService(IConfiguration configuration)
    {
        _configuration = configuration;

        var encryptionKey = _configuration["Encryption:Key"] ?? _configuration["LlmEncryption:Key"];
        _configIv = _configuration["Encryption:IV"];

        if (string.IsNullOrEmpty(encryptionKey))
            throw new InvalidOperationException("Encryption Key not configured (checked Encryption:Key and LlmEncryption:Key)");

        // Support both base64-encoded keys and raw UTF-8 key strings (legacy tests use a 32-char plaintext key)
        try
        {
            _key = Convert.FromBase64String(encryptionKey);
        }
        catch (FormatException)
        {
            _key = Encoding.UTF8.GetBytes(encryptionKey);
        }

        // If IV is empty or not provided, treat as zero IV (legacy behavior in tests)
        if (string.IsNullOrEmpty(_configIv))
        {
            _iv = new byte[16];
        }
        else
        {
            try
            {
                _iv = Convert.FromBase64String(_configIv);
            }
            catch (FormatException)
            {
                _iv = Encoding.UTF8.GetBytes(_configIv);
            }
        }

        if (_key.Length != 32)
            throw new ArgumentException("Key must be 32 bytes (256 bits) for AES-256", nameof(encryptionKey));

        if (_iv.Length != 16)
            throw new ArgumentException("IV must be 16 bytes (128 bits)", nameof(_configIv));
    }

    // Backwards-compatible constructor used by older tests that passed a logger
    public EncryptionService(IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<EncryptionService> _)
        : this(configuration)
    {
    }

    public byte[] Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));

        using (Aes aes = Aes.Create())
        {
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    cryptoStream.FlushFinalBlock();
                    return memoryStream.ToArray();
                }
            }
        }
    }

    public string Decrypt(byte[] cipherText)
    {
        if (cipherText == null || cipherText.Length == 0)
            throw new ArgumentNullException(nameof(cipherText));

        using (Aes aes = Aes.Create())
        {
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new MemoryStream(cipherText))
            {
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader streamReader = new StreamReader(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }
    }
}
