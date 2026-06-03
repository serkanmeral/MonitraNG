using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Services;
using MngWorkflow.Infrastructure.Services;

namespace MngWorkflow.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MngWorkflowSettings>(configuration.GetSection(MngWorkflowSettings.SectionName));

        services.AddHttpClient<IDataGatewayClient, DataGatewayClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IValidationPipelineService, ValidationPipelineService>();
        services.AddWorkflowEngineCore(configuration);

        return services;
    }
}
