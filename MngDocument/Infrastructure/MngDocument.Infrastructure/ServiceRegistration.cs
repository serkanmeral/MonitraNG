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

        var rendering = settings.DocumentRendering;
        if (rendering.Enabled && !string.IsNullOrWhiteSpace(rendering.GotenbergBaseUrl))
        {
            services.AddHttpClient("Gotenberg", client =>
            {
                client.BaseAddress = new Uri(rendering.GotenbergBaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(rendering.TimeoutSeconds, 30, 600));
            });
        }

        services.AddHttpClient("MngKeeper", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IMngDataGatewayClient, MngDataGatewayClient>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IResourceLinkService, ResourceLinkService>();
        services.AddScoped<ITemplateCategoryService, TemplateCategoryService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
        services.AddScoped<ITemplateEditorService, TemplateEditorService>();
        services.AddScoped<IDomainLogoProvider, DomainLogoProvider>();
        services.AddScoped<ITemplateLetterheadApplier, TemplateLetterheadApplier>();
        services.AddScoped<ITemplateFooterApplier, TemplateFooterApplier>();
        services.AddScoped<ITemplateBrandingApplier, TemplateBrandingApplier>();
        services.AddScoped<IDocumentRenderService, GotenbergDocumentRenderService>();
        services.AddSingleton<IWopiSessionStore, InMemoryWopiSessionStore>();

        return services;
    }
}
