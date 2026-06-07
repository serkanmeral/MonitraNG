using Microsoft.Extensions.DependencyInjection;
using MngNotifier.Application.Services;
using MngNotifier.Infrastructure.Clients;
using MngNotifier.Infrastructure.Services;

namespace MngNotifier.Infrastructure;

public static class ServiceRegistration
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpClient("MngDataGateway");

        services.AddScoped<IMailProvider, SmtpMailProvider>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IDataGatewayTemplateClient, DataGatewayTemplateClient>();
        services.AddScoped<ITemplateRenderService, TemplateRenderService>();
    }
}
