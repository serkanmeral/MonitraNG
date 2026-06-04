using MngEngine.Application.Collector.Common;
using MngEngine.Domain.Entities.Asset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Interfaces
{
    public interface IAssetService
    {
        Task<List<AssetInfo>> GetAssetsAsync();

        /// <param name="periodExpression">Belirtilirse sadece bu period'a sahip asset'ler döner. null ise tümü.</param>
        Task<List<BaseCollectorRequest>> GetCollectorRequests(string? periodExpression = null);
    }
}