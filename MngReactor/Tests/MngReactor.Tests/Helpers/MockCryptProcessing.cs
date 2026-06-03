using MngReactor.Application.Abstractions.Crypt;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// ICryptProcessing mock - Encrypt("x") -> "ENC:x" doner.
/// connection_info sifreleme testlerinde plaintext gondermediginizi dogrulamak icin.
/// </summary>
public class MockCryptProcessing : ICryptProcessing
{
    public const string EncryptedPrefix = "ENC:";

    public Task<string> CreateKeyFile() => Task.FromResult("mock-key");

    public Task<string> Encrypt(string text)
    {
        if (string.IsNullOrEmpty(text)) return Task.FromResult(text);
        return Task.FromResult(EncryptedPrefix + text);
    }

    public Task<string> Decrypt(string text)
    {
        if (string.IsNullOrEmpty(text)) return Task.FromResult(text);
        return text.StartsWith(EncryptedPrefix)
            ? Task.FromResult(text[EncryptedPrefix.Length..])
            : Task.FromResult(text);
    }

    public Task<byte[]> Compress(string text) => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(text));

    public Task<string> DeCompress(byte[] text) => Task.FromResult(System.Text.Encoding.UTF8.GetString(text));
}
