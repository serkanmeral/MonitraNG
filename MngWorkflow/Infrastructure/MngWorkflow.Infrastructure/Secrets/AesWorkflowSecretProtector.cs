using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;

namespace MngWorkflow.Infrastructure.Secrets;

public interface IWorkflowSecretProtector
{
    string Protect(string plainText);
    string Unprotect(string cipherText);
    bool IsConfigured { get; }
}

public sealed class AesWorkflowSecretProtector : IWorkflowSecretProtector
{
    private readonly byte[]? _key;

    public AesWorkflowSecretProtector(IOptions<MngWorkflowSettings> settings)
    {
        var keyB64 = settings.Value.Secrets.EncryptionKeyBase64;
        if (!string.IsNullOrWhiteSpace(keyB64))
        {
            _key = Convert.FromBase64String(keyB64);
            if (_key.Length != 32)
                throw new InvalidOperationException("Secrets.EncryptionKeyBase64 must decode to 32 bytes (AES-256).");
        }
    }

    public bool IsConfigured => _key != null;

    public string Protect(string plainText)
    {
        if (_key == null)
            throw new InvalidOperationException("Workflow secret encryption is not configured.");

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        var payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string cipherText)
    {
        if (_key == null)
            throw new InvalidOperationException("Workflow secret encryption is not configured.");

        var payload = Convert.FromBase64String(cipherText);
        if (payload.Length < 28)
            throw new InvalidOperationException("Invalid secret payload.");

        var nonce = payload[..12];
        var tag = payload[12..28];
        var cipher = payload[28..];

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
