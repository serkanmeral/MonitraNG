using Microsoft.Extensions.DependencyInjection;
using MngLLM.Application.Configuration;
using MngLLM.Domain.Interfaces;
using MngLLM.Infrastructure.Adapters;
using MngLLM.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace MngLLM.Infrastructure;

public static class ServiceRegistration
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        // Register LLM Service (Ollama adapter)
        services.AddScoped<ILLMService, OllamaLLMAdapter>();
        
        // Register Documentation Provider
        services.AddSingleton<IDocumentationProvider, DocumentationProvider>();
        
        // Register Context Manager
        services.AddSingleton<IContextManager, ContextManager>();
        
        // Add HTTP Client Factory for OpenAPI requests
        services.AddHttpClient();
        
        // Add Memory Cache for caching
        services.AddMemoryCache();
    }
}
