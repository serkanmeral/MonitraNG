using Microsoft.Extensions.DependencyInjection;
using MngDocument.Application.Configuration;

namespace MngDocument.Application;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        MngDocumentSettings settings)
    {
        services.Configure<MngDocumentSettings>(s =>
        {
            s.Server = settings.Server;
            s.OpenApiServerPath = settings.OpenApiServerPath;
            s.DataGateway = settings.DataGateway;
            s.Resources = settings.Resources;
        });

        return services;
    }
}
