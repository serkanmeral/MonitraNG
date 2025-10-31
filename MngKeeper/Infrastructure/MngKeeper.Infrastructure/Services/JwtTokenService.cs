using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Infrastructure.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(ILogger<JwtTokenService> logger)
        {
            _logger = logger;
        }

        public string AddDomainClaimToToken(string originalToken, string domainId, string domainName, bool isAdmin = false)
        {
            try
            {
                _logger.LogInformation("Adding domain claim to token for domain: {DomainName}, isAdmin: {IsAdmin}", domainName, isAdmin);

                // Parse the original token
                var tokenParts = originalToken.Split('.');
                if (tokenParts.Length != 3)
                {
                    _logger.LogWarning("Invalid JWT token format");
                    return originalToken;
                }

                // Decode the payload
                var payload = DecodeJwtPart(tokenParts[1]);
                var payloadJson = JsonSerializer.Deserialize<JsonElement>(payload);

                // Create new payload with domain claims
                var newPayload = new Dictionary<string, object>();

                // Copy all existing claims
                foreach (var property in payloadJson.EnumerateObject())
                {
                    newPayload[property.Name] = property.Value;
                }

                // Add domain claims
                newPayload["domain_id"] = domainId;
                newPayload["domain_name"] = domainName;
                newPayload["domain_realm"] = domainName.ToLower().Replace(" ", "_");
                newPayload["is_admin"] = isAdmin;

                // Serialize the new payload
                var newPayloadJson = JsonSerializer.Serialize(newPayload);
                var newPayloadBase64 = EncodeJwtPart(newPayloadJson);

                // Create new token (we'll keep the same header and signature for now)
                var newToken = $"{tokenParts[0]}.{newPayloadBase64}.{tokenParts[2]}";

                _logger.LogInformation("Domain claim added to token successfully for domain: {DomainName}, isAdmin: {IsAdmin}", domainName, isAdmin);

                return newToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding domain claim to token for domain: {DomainName}", domainName);
                return originalToken; // Return original token if there's an error
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

        private string EncodeJwtPart(string part)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(part);
            var base64 = Convert.ToBase64String(bytes);
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
