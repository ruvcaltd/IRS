using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using IRS.LLM.Services;

namespace IRS.ClientGeneration.Tests;

[TestFixture]
public class EncryptionServiceTests
{
    private IEncryptionService _svc = null!;

    [SetUp]
    public void Setup()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] {
                new KeyValuePair<string, string?>("Encryption:Key", "0123456789ABCDEF0123456789ABCDEF"), // 32 chars
                new KeyValuePair<string, string?>("Encryption:IV", "")
            })
            .Build();

        _svc = new EncryptionService(cfg, new NullLogger<EncryptionService>());
    }

    [Test]
    public void EncryptDecrypt_Roundtrip_variousLengths()
    {
        var rnd = new Random(1234);
        for (int len = 0; len <= 128; len++)
        {
            // build a reproducible string of `len` (mix of ascii)
            var sb = new StringBuilder();
            while (sb.Length < len) sb.Append(((char)('A' + (sb.Length % 26))));
            var plain = sb.ToString().Substring(0, len);

            var ct = _svc.Encrypt(plain);
            var outp = _svc.Decrypt(ct);
            Assert.That(outp, Is.EqualTo(plain), $"mismatch for len={len}");
        }
    }

    [Test]
    public void Decrypt_Accepts_LegacyCiphertext()
    {
        var plain = "LegacyPlain123!";

        // Simulate legacy encryption: use configured key + configured (zero) IV (no prefix)
        var key = Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF");
        var iv = new byte[16]; // configured IV is empty => zero IV

        byte[] legacyCt;
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            using var enc = aes.CreateEncryptor(aes.Key, aes.IV);
            var pt = Encoding.UTF8.GetBytes(plain);
            legacyCt = enc.TransformFinalBlock(pt, 0, pt.Length);
        }

        // Ensure decrypt still works for legacy ciphertext
        var dec = _svc.Decrypt(legacyCt);
        Assert.That(dec, Is.EqualTo(plain));
    }


}
