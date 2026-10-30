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
            s.DocumentRendering = settings.DocumentRendering;
            s.Collabora = settings.Collabora;
            s.Wopi = settings.Wopi;
            s.EditorLimits = settings.EditorLimits;
            s.EditorLock = settings.EditorLock;
            s.Keeper = settings.Keeper;
            s.FooterProfile = settings.FooterProfile;
            s.LegacyOdakFooterEnabled = settings.LegacyOdakFooterEnabled;
            s.DocumentGeneration = settings.DocumentGeneration;
            s.Notifications = settings.Notifications;
            s.OpenSearch = settings.OpenSearch;
            s.Summary = settings.Summary;
            s.Antivirus = settings.Antivirus;
        });

        return services;
    }
}
