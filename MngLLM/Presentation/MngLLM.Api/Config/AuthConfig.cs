using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using MngLLM.Application.Configuration;

namespace MngLLM.Api.Config;

public static class AuthConfig
{
    public static void AddAuthentication(this IServiceCollection services, MngLLMSettings settings)
    {
        // JWT: DG ile aynı model. Token MngKeeper'dan gelir; MngKeeper Keycloak token'ına domain_name vb.
        // ekler ama imzayı yenilemez (signature invalid olur). Bu yüzden Authority ile metadata çekmek
        // hem başarısız olur hem de realm sabit olur. Domain değişkendir, domain_name token içinden okunur.
        // Authority set etmiyoruz → metadata fetch yok; sadece token parse + claim okuma (DG gibi).
        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                if (!string.IsNullOrWhiteSpace(settings.Jwt.Authority))
                {
                    options.Authority = settings.Jwt.Authority.TrimEnd('/');
                    options.RequireHttpsMetadata = false;
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = delegate { return true; }
                    };
                }
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = false,
                    SignatureValidator = delegate (string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters)
                    {
                        var jwt = new JsonWebToken(token);
                        return jwt;
                    }
                };
            });
        
        // Authorization policies
        services.AddAuthorization();
    }
}
