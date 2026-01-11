using Microsoft.Extensions.DependencyInjection;
using MngLLM.Application.Configuration;
using MngLLM.Domain.Interfaces;
using MngLLM.Infrastructure.Adapters;
using Microsoft.Extensions.Options;

namespace MngLLM.Infrastructure;

public static class ServiceRegistration
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        // Register LLM Service (Ollama adapter)
        services.AddScoped<ILLMService, OllamaLLMAdapter>();
    }
}
