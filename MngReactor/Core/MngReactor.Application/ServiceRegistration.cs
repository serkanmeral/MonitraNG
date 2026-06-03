using Microsoft.Extensions.DependencyInjection;
using MngReactor.Application.Configuration;

namespace MngReactor.Application;

public static class ServiceRegistration
{
    public static void AddApplicationServices(this IServiceCollection services, MngReactorSettings settings)
    {
        services.Configure<MngReactorSettings>(_ => { });
        services.AddSingleton(settings);

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(ServiceRegistration).Assembly);
        });
    }
}