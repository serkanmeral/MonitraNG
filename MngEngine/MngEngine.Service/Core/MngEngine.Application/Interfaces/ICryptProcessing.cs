using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Interfaces
{
    public interface ICryptProcessing
    {
        Task<string> Encrypt(string text);
        Task<string> Decrypt(string text);
        Task<byte[]> Compress(string text);
        /// <summary>GZip sıkıştırma + AES şifreleme (Ingest için). key=compressPrk, iv=compressPbk.</summary>
        Task<byte[]> CompressAndEncrypt(string plainText, string compressPrk, string compressPbk);
        Task<string> DeCompress(byte[] text, string compressPrk, string compressPbk);
    }
}
