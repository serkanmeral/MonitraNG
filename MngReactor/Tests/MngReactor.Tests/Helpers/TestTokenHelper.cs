using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// IngestController gibi JWT parse eden endpoint'ler icin gecerli test token uretir.
/// </summary>
public static class TestTokenHelper
{
    private const string TestSecret = "test-secret-at-least-32-chars-long!!";

    public static string CreateBearerToken(string domain = "meral")
    {
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(TestSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("domain_name", domain),
                new Claim("domain", domain),
                new Claim("preferred_username", "testuser"),
                new Claim("username", "testuser")
            },
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
