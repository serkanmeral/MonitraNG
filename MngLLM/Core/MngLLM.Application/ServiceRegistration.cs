using Microsoft.Extensions.DependencyInjection;
using MngLLM.Application.Configuration;

namespace MngLLM.Application;

public static class ServiceRegistration
{
    public static void AddApplicationServices(
        this IServiceCollection services,
        MngLLMSettings mngLLMSettings)
    {
        // Configuration
        services.Configure<MngLLMSettings>(_ =>
        {
            _.Server = mngLLMSettings.Server;
            _.Ollama = mngLLMSettings.Ollama;
            _.Translation = mngLLMSettings.Translation;
            _.OpenApiServerPath = mngLLMSettings.OpenApiServerPath;
            _.CertificateSettings = mngLLMSettings.CertificateSettings;
            _.Actors = mngLLMSettings.Actors;
            _.Documentation = mngLLMSettings.Documentation;
        });

        // Add MediatR
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(ServiceRegistration).Assembly);
        });

        // Add Memory Cache for translations
        services.AddMemoryCache();
        
        // Add HttpClient Factory
        services.AddHttpClient();
    }
}
