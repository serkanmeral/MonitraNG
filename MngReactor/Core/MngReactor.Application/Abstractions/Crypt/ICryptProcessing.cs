using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Application.Abstractions.Crypt
{
    public interface ICryptProcessing
    {
        Task<string> CreateKeyFile();
        Task<string> Encrypt(string text);
        Task<string> Decrypt(string text);
        Task<byte[]> Compress(string text);
        Task<string> DeCompress(byte[] text);
    }
}
