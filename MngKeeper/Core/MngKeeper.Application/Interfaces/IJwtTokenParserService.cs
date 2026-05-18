namespace MngKeeper.Application.Interfaces
{
    public interface IJwtTokenParserService
    {
        TokenClaims? ParseToken(string token);
    }

    public class TokenClaims
    {
        /// <summary>Keycloak kullanıcı id (JWT <c>sub</c>).</summary>
        public string? UserId { get; set; }

        /// <summary>Keeper domain DB <c>@users</c> kaydı Mongo id (JWT <c>mng_person_id</c>).</summary>
        public string? MngPersonId { get; set; }

        public string? DomainId { get; set; }
        public string? DomainName { get; set; }
        public string? DomainRealm { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public List<string> Groups { get; set; } = new();
    }
}
