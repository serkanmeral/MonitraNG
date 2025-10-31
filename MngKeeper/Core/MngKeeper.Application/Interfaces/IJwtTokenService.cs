namespace MngKeeper.Application.Interfaces
{
    public interface IJwtTokenService
    {
        string AddDomainClaimToToken(string originalToken, string domainId, string domainName, bool isAdmin = false);
    }
}
