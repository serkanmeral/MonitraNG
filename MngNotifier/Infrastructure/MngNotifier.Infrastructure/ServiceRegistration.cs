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
        services.AddHttpClient("MngKeeper");
        services.AddHttpClient("TelegramBot", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IMailProvider, SmtpMailProvider>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IDataGatewayTemplateClient, DataGatewayTemplateClient>();
        services.AddScoped<ITemplateRenderService, TemplateRenderService>();
        services.AddScoped<IMessageTemplateRenderService, MessageTemplateRenderService>();
        services.AddScoped<ITelegramMessageSender, TelegramBotMessageSender>();
        services.AddScoped<ITelegramUpdateProcessor, TelegramUpdateProcessor>();
        services.AddHostedService<TelegramWebhookRegistrationHostedService>();
        services.AddHostedService<TelegramPollingHostedService>();
    }
}
