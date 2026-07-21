using Microsoft.Extensions.DependencyInjection;
using MngLLM.Application.Configuration;
using MngLLM.Application.Services;
using MngLLM.Domain.Interfaces;
using MngLLM.Infrastructure.Adapters;
using MngLLM.Infrastructure.Clients;
using MngLLM.Infrastructure.Services;
using MngLLM.Infrastructure.Services.Di;

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

        // DI Auto-Extract (UBL mapper; JSON only — no DB persist)
        services.AddScoped<IUblEarsivFaturaMapper, UblEarsivFaturaMapper>();
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddScoped<ILlmEarsivFaturaExtractor, LlmEarsivFaturaExtractor>();
        services.AddScoped<IDocumentIntelligenceClient, DocumentIntelligenceClient>();
        services.AddScoped<IDiExtractService, DiExtractService>();
        services.AddSingleton<ILlmKeeperAuthClient, LlmKeeperAuthClient>();
        
        // Add HTTP Client Factory for OpenAPI / Document / DG / Keeper requests
        services.AddHttpClient();
        
        // Add Memory Cache for caching
        services.AddMemoryCache();
    }
}
