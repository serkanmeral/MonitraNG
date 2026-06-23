using Microsoft.Extensions.DependencyInjection;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Infrastructure.Clients;
using MngDocument.Infrastructure.Services;

namespace MngDocument.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        MngDocumentSettings settings)
    {
        services.AddHttpClient("MngDataGateway", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddScoped<IMngDataGatewayClient, MngDataGatewayClient>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IResourceLinkService, ResourceLinkService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();

        return services;
    }
}
