using Microsoft.Extensions.DependencyInjection;
using MngNotifier.Application.Services;
using MngNotifier.Infrastructure.Services;

namespace MngNotifier.Infrastructure;

public static class ServiceRegistration
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        // Mail Provider
        services.AddScoped<IMailProvider, SmtpMailProvider>();
    }
}
