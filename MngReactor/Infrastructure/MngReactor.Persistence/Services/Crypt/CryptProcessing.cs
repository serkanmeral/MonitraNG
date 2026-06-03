using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Persistence.Settings;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Persistence.Services.Crypt
{
    public class CryptProcessing : ICryptProcessing
    {
        private readonly IOptions<MngReactorSettings> _options;

        public CryptProcessing(IOptions<MngReactorSettings> options)
        {
            _options = options;
        }
        private byte[] CompressString(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    gzipStream.Write(bytes, 0, bytes.Length);
                }

                return memoryStream.ToArray();
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

        private byte[] EncryptCompress(byte[] data, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(data, 0, data.Length);
                        cryptoStream.FlushFinalBlock();
                    }

                    return memoryStream.ToArray();
                }
            }
        }

        private byte[] DecryptCompress(byte[] encryptedData, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var memoryStream = new MemoryStream(encryptedData))
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                using (var resultStream = new MemoryStream())
                {
                    cryptoStream.CopyTo(resultStream);
                    return resultStream.ToArray();
                }
            }
        }

        public async Task<byte[]> Compress(string text)
        {
            byte[] compressedText = CompressString(text);

            byte[] key = Encoding.UTF8.GetBytes(_options.Value.CompressPrk); // 16 bytes for AES-128
            byte[] iv = Encoding.UTF8.GetBytes(_options.Value.CompressPbk);  // 16 bytes for AES

            // Şifreleme
            byte[] encryptedText = EncryptCompress(compressedText, key, iv);

            return encryptedText;
        }

        public Task<string> CreateKeyFile()
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;

                // Public ve private key oluşturuluyor
                var privateKey = rsa.ExportRSAPrivateKey();
                var publicKey = rsa.ExportRSAPublicKey();

                // Private key dosyası kaydediliyor
                File.WriteAllBytes("privateKey.pem", privateKey);
                File.WriteAllBytes("publicKey.pem", publicKey);
            }

            return Task.FromResult("Ok");
        }

        public async Task<string> DeCompress(byte[] text)
        {
            byte[] key = Encoding.UTF8.GetBytes(_options.Value.CompressPrk); // 16 bytes for AES-128
            byte[] iv = Encoding.UTF8.GetBytes(_options.Value.CompressPbk);  // 16 bytes for AES

            var cmpData = DecryptCompress(text, key, iv);

            var resData = DecompressString(cmpData);

            return resData;
        }

        public Task<string> Decrypt(string text)
        {
            string encryptedText = text;
            byte[] decryptedText;

            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;

                // Private key yükleniyor
                var privateKey = File.ReadAllBytes("privateKey.pem");
                rsa.ImportRSAPrivateKey(privateKey, out _);

                // Şifreyi çözme
                decryptedText = rsa.Decrypt(Convert.FromBase64String(encryptedText), RSAEncryptionPadding.Pkcs1);
            }

            return Task.FromResult(Encoding.UTF8.GetString(decryptedText));
        }

        public Task<string> Encrypt(string text)
        {
            string originalText = text;
            byte[] encryptedText;

            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;

                // Public key yükleniyor
                var publicKey = File.ReadAllBytes("publicKey.pem");
                rsa.ImportRSAPublicKey(publicKey, out _);

                // Veriyi şifreleme
                encryptedText = rsa.Encrypt(Encoding.UTF8.GetBytes(originalText), RSAEncryptionPadding.Pkcs1);
            }

            return Task.FromResult(Convert.ToBase64String(encryptedText));
        }


    }
}
