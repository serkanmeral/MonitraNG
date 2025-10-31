namespace MngKeeper.Application.Interfaces
{
    public interface IJwtTokenParserService
    {
        TokenClaims? ParseToken(string token);
    }

    public class TokenClaims
    {
        public string? DomainId { get; set; }
        public string? DomainName { get; set; }
        public string? DomainRealm { get; set; }
        public bool IsAdmin { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }
}
