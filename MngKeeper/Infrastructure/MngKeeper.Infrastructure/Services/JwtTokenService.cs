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

        public string AddDomainClaimToToken(
            string originalToken, 
            string domainId, 
            string domainName, 
            bool isAdmin = false, 
            bool isManager = false,
            List<string>? userGroups = null,
            string? title = null,
            string? department = null,
            int? gender = null,
            string? phoneNumber = null,
            string? photoUrl = null)
        {
            try
            {
                _logger.LogInformation("Adding domain claim to token for domain: {DomainName}, isAdmin: {IsAdmin}, isManager: {IsManager}, userGroups: {UserGroups}", 
                    domainName, isAdmin, isManager, userGroups != null ? string.Join(", ", userGroups) : "null");

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
                newPayload["is_manager"] = isManager; // snake_case for consistency with is_admin

                // Add user_groups claim if provided
                if (userGroups != null && userGroups.Count > 0)
                {
                    newPayload["user_groups"] = userGroups;
                    _logger.LogInformation("Added user_groups to token: {UserGroups}", string.Join(", ", userGroups));
                }
                else
                {
                    // Ensure user_groups is always present, even if empty
                    newPayload["user_groups"] = new List<string>();
                    _logger.LogInformation("Added empty user_groups array to token");
                }

                // Add user profile fields if provided
                if (!string.IsNullOrEmpty(title))
                {
                    newPayload["title"] = title;
                    _logger.LogInformation("Added title to token: {Title}", title);
                }
                if (!string.IsNullOrEmpty(department))
                {
                    newPayload["department"] = department;
                    _logger.LogInformation("Added department to token: {Department}", department);
                }
                if (gender.HasValue)
                {
                    newPayload["gender"] = gender.Value;
                    _logger.LogInformation("Added gender to token: {Gender}", gender.Value);
                }
                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    newPayload["phoneNumber"] = phoneNumber;
                    _logger.LogInformation("Added phoneNumber to token: {PhoneNumber}", phoneNumber);
                }
                if (!string.IsNullOrEmpty(photoUrl))
                {
                    newPayload["photoUrl"] = photoUrl;
                    _logger.LogInformation("Added photoUrl to token: {PhotoUrl}", photoUrl);
                }

                _logger.LogInformation("Adding claims - isManager: {IsManager}, userGroups: {UserGroups}, title: {Title}, department: {Department}, gender: {Gender}, phoneNumber: {PhoneNumber}, photoUrl: {PhotoUrl}", 
                    isManager, userGroups != null ? string.Join(", ", userGroups) : "null", title ?? "null", department ?? "null", gender?.ToString() ?? "null", phoneNumber ?? "null", photoUrl ?? "null");

                // Serialize the new payload
                var newPayloadJson = JsonSerializer.Serialize(newPayload);
                var newPayloadBase64 = EncodeJwtPart(newPayloadJson);

                // Create new token (we'll keep the same header and signature for now)
                // NOTE: This will invalidate the signature, but the token will still be readable
                // For production, we should re-sign the token with our private key
                var newToken = $"{tokenParts[0]}.{newPayloadBase64}.{tokenParts[2]}";

                _logger.LogInformation("Domain claim added to token successfully for domain: {DomainName}, isAdmin: {IsAdmin}, isManager: {IsManager}", 
                    domainName, isAdmin, isManager);

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
