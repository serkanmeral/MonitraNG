using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Domain.Entities.Login
{
    public class UserInfoModel
    {
        public string userName { get; set; }
        public string domain { get; set; }
        public string accessToken { get; set; }
    }
}
