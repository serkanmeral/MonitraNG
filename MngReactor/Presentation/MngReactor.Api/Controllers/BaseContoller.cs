using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Domain.Entities.Login;
using System.IdentityModel.Tokens.Jwt;

namespace MngReactor.Api.Controllers
{
    public class BaseContoller : ControllerBase
    {
        public BaseContoller()
        {
        }

        /// <summary>
        /// Kullanıcı bilgisini alır. Önce User.Claims (Bearer), sonra auth.Properties (Cookie/session) dener.
        /// </summary>
        internal async Task<dynamic> GetUserInfo()
        {
            string? domain = User.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
            string? userName = User.Claims.FirstOrDefault(c => c.Type == "username" || c.Type == "preferred_username")?.Value;
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            string? accessToken = authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..]
                : null;

            if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(accessToken))
                return new UserInfoModel { userName = userName ?? "", domain = domain ?? "", accessToken = accessToken ?? "" };

            var authObj = await HttpContext.AuthenticateAsync();
            var authenticationProperties = authObj.Properties?.Items;
            if (authenticationProperties != null)
            {
                accessToken = authenticationProperties.FirstOrDefault(x => x.Key == ".Token.access_token").Value ?? "";
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
                    domain = token.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
                    userName = token.Claims.FirstOrDefault(c => c.Type == "username" || c.Type == "preferred_username")?.Value;
                    return new UserInfoModel { userName = userName ?? "", domain = domain ?? "", accessToken = accessToken };
                }
            }

            return new UserInfoModel { userName = "", domain = "", accessToken = "" };
        }

        internal async Task<JwtSecurityToken> GetJwtSecurityToken()
        {
            var authenticationProperties = (await HttpContext.AuthenticateAsync()).Properties.Items;
            string accessToken = authenticationProperties.FirstOrDefault(x => x.Key == ".Token.access_token").Value;

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(accessToken);

            //var claims = token.Claims.Select(claim => (claim.Type, claim.Value)).ToList();

            return token;
        }
    }
}