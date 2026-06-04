using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Interfaces
{
    public interface IConfigService
    {
        /// <summary>Config uygular. Başarısızsa (false, hata mesajı) döner.</summary>
        Task<(bool Success, string? ErrorMessage)> ApplyConfig(string configText);
        /// <summary>Config'i siler; Engine sıfır kurulum moduna geçer.</summary>
        Task ClearConfigAsync();
        Task InitConfig();
    }
}
