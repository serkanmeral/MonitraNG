using Microsoft.Extensions.Options;
using MngEngine.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Persistence.Service.Crypt
{
    public class CryptProcessing : ICryptProcessing
    {
        private const string DefaultPrivateKeyPath = "privateKey.pem";
        public Task<byte[]> Compress(string text)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> CompressAndEncrypt(string plainText, string compressPrk, string compressPbk)
        {
            var compressed = CompressString(Encoding.UTF8.GetBytes(plainText));
            var key = Encoding.UTF8.GetBytes(compressPrk);
            var iv = Encoding.UTF8.GetBytes(compressPbk);
            var encrypted = EncryptAes(compressed, key, iv);
            return Task.FromResult(encrypted);
        }

        private static byte[] CompressString(byte[] bytes)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionMode.Compress))
                gz.Write(bytes, 0, bytes.Length);
            return ms.ToArray();
        }

        private static byte[] EncryptAes(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                cs.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private byte[] DecryptCompress(byte[] encryptedData, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                try
                {
                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    using (var memoryStream = new MemoryStream(encryptedData))
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    using (var resultStream = new MemoryStream())
                    {
                        cryptoStream.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    return new byte[0];
                }

            }
        }

        private string DecompressString(byte[] compressedBytes)
        {
            using (var compressedStream = new MemoryStream(compressedBytes))
            {
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        gzipStream.CopyTo(resultStream);
                        byte[] resultBytes = resultStream.ToArray();
                        return Encoding.UTF8.GetString(resultBytes);
                    }
                }
            }
        }

        public async Task<string> DeCompress(byte[] text, string compressPrk, string compressPbk)
        {
            byte[] key = Encoding.UTF8.GetBytes(compressPrk); // 16 bytes for AES-128
            byte[] iv = Encoding.UTF8.GetBytes(compressPbk);  // 16 bytes for AES

            var cmpData = DecryptCompress(text, key, iv);

            var resData = DecompressString(cmpData);

            return resData;
        }

        public Task<string> Decrypt(string text)
        {
            var s = (text ?? "").Trim();
            s = s.Replace(' ', '+');
            if (string.IsNullOrEmpty(s))
                return Task.FromResult("");

            // Test/dev: CompressPbk/CompressPrk bazen plain gönderilir (16 hex/ascii; AES key/IV).
            // RSA ciphertext Base64 ~344 karakter olur. Kısa string = plain key; RSA atla.
            if (s.Length < 100)
                return Task.FromResult(s);

            var path = DefaultPrivateKeyPath;
            if (!File.Exists(path))
            {
                var cwd = Directory.GetCurrentDirectory();
                throw new InvalidOperationException(
                    $"Config şifresini çözmek için private key dosyası bulunamadı: '{path}'. Çalışma dizini: {cwd}. " +
                    "Engine'ın Reactor ile aynı RSA anahtar çiftini kullandığından emin olun (Reactor publicKey.pem, Engine privateKey.pem).");
            }

            var privateKey = File.ReadAllBytes(path);
            byte[] decryptedText;
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportRSAPrivateKey(privateKey, out _);
                decryptedText = rsa.Decrypt(Convert.FromBase64String(s), RSAEncryptionPadding.Pkcs1);
            }
            return Task.FromResult(Encoding.UTF8.GetString(decryptedText));
        }

        public Task<string> Encrypt(string text)
        {
            throw new NotImplementedException();
        }

    }
}
