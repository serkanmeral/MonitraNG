using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using MngLLM.Application.Configuration;

namespace MngLLM.Api.Config;

public static class AuthConfig
{
    public static void AddAuthentication(this IServiceCollection services, MngLLMSettings settings)
    {
        // JWT Authentication (MngDataGateway pattern)
        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.Authority = settings.Actors.MngKeeper;
                options.RequireHttpsMetadata = false; // Development için
                
                // SSL certificate validation bypass (development)
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = delegate { return true; }
                };
                
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
