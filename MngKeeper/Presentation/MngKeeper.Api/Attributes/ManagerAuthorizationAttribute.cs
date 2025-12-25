using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Attributes
{
    /// <summary>
    /// Authorization attribute that allows both Admin and Manager users
    /// Admin users automatically have Manager privileges
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ManagerAuthorizationAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var jwtTokenParserService = context.HttpContext.RequestServices.GetService<IJwtTokenParserService>();
            if (jwtTokenParserService == null)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "JWT token parser service not available" });
                return;
            }

            // Get token from Authorization header
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Authorization header missing or invalid" });
                return;
            }

            var token = authHeader.Substring("Bearer ".Length);
            var claims = jwtTokenParserService.ParseToken(token);
            
            if (claims == null)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid token" });
                return;
            }

            // Allow if user is Admin OR Manager
            if (!claims.IsAdmin && !claims.IsManager)
            {
                context.Result = new ForbidObjectResult(new { error = "Manager or Admin privileges required" });
                return;
            }

            // Store claims in HttpContext for later use
            context.HttpContext.Items["TokenClaims"] = claims;
        }
    }
}

