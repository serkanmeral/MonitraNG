using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Infrastructure.Services
{
    public class JwtTokenParserService : IJwtTokenParserService
    {
        private readonly ILogger<JwtTokenParserService> _logger;

        public JwtTokenParserService(ILogger<JwtTokenParserService> logger)
        {
            _logger = logger;
        }

        public TokenClaims? ParseToken(string token)
        {
            try
            {
                _logger.LogInformation("Parsing JWT token");
                
                var tokenParts = token.Split('.');
                if (tokenParts.Length != 3)
                {
                    _logger.LogWarning("Invalid JWT token format");
                    return null;
                }

                var payload = DecodeJwtPart(tokenParts[1]);
                var payloadJson = JsonSerializer.Deserialize<JsonElement>(payload);
                
                var claims = new TokenClaims();
                
                // Extract standard claims
                if (payloadJson.TryGetProperty("preferred_username", out var usernameElement))
                    claims.Username = usernameElement.GetString();
                
                if (payloadJson.TryGetProperty("email", out var emailElement))
                    claims.Email = emailElement.GetString();
                
                // Extract custom domain claims
                if (payloadJson.TryGetProperty("domain_id", out var domainIdElement))
                    claims.DomainId = domainIdElement.GetString();
                
                if (payloadJson.TryGetProperty("domain_name", out var domainNameElement))
                    claims.DomainName = domainNameElement.GetString();
                
                if (payloadJson.TryGetProperty("domain_realm", out var domainRealmElement))
                    claims.DomainRealm = domainRealmElement.GetString();
                
                if (payloadJson.TryGetProperty("is_admin", out var isAdminElement))
                    claims.IsAdmin = isAdminElement.GetBoolean();
                
                _logger.LogInformation("Token parsed successfully for user: {Username}, domain: {DomainName}, isAdmin: {IsAdmin}", 
                    claims.Username, claims.DomainName, claims.IsAdmin);
                
                return claims;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JWT token");
                return null;
            }
        }

        private string DecodeJwtPart(string part)
        {
            var padding = 4 - (part.Length % 4);
            if (padding != 4)
            {
                part += new string('=', padding);
            }
            var bytes = Convert.FromBase64String(part.Replace('-', '+').Replace('_', '/'));
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }
}
