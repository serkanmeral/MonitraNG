using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Domain.Entities.Asset
{
    public record ConnectionInfo
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
    public record Collectible
    {
        public string Name { get; set; }
        public string CType { get; set; }
        public string Code { get; set; }
        public List<Collectible> Options { get; set; }
    }
    public record AssetInfo
    {
        public string Domain { get; set; }
        public string Asset_Name { get; set; }
        public string Asset_Id { get; set; }
        public string Asset_Type_Name { get; set; }
        public string Asset_Type_Id { get; set; }
        public string Asset_Sub_Type_Name { get; set; }
        public string Asset_Sub_Type_Id { get; set; }
        public string ParentId { get; set; }
        public List<Collectible> CollectibleItems { get; set; }
        public ConnectionInfo ConnectionInfo { get; set; }
    }
}