using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Domain.Entities.Asset
{
    public record AssetType
    {
        public string __dataId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<AssetCollectibleInfo> Collectibles { get; set; }
    }
    public record AssetCollectibleInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<string> Search { get; set; }
    }
}