using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Interfaces
{
    public interface IInitApplicationService
    {
        Task InitApplication();
        /// <summary>Config uygulandıktan sonra Reactor'dan sync alır ve job'ları günceller. UI'da anında agent/asset görüntülenebilir.</summary>
        Task RunConfigSyncAndRescheduleAsync();
    }
}
