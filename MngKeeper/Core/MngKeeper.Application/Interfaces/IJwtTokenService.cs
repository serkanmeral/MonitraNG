namespace MngKeeper.Application.Interfaces
{
    public interface IJwtTokenService
    {
        string AddDomainClaimToToken(
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
            string? photoUrl = null);
    }
}
