namespace IRS.LLM.Services;

public interface IEncryptionService
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] cipherText);
}
