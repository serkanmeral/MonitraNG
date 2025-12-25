using Microsoft.Extensions.DependencyInjection;
using MngDataGateway.Application.Interfaces;
using MngDataGateway.Application.Services;
using MngDataGateway.Infrastructure.Services;
using MngDataGateway.Infrastructure.Services.RabbitMq;

namespace MngDataGateway.Infrastructure
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // RabbitMQ Service (Singleton - one connection per app instance)
            services.AddSingleton<IRabbitMqService, RabbitMqService>();

            // Event Publisher (MngKeeper-style) - Scoped for per-request usage
            services.AddScoped<IEventPublisher, EventPublisher>();

            return services;
        }
    }
}

