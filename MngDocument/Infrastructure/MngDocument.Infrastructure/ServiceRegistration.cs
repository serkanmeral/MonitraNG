using Microsoft.Extensions.DependencyInjection;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Infrastructure.Clients;
using MngDocument.Infrastructure.Services;
using MngDocument.Infrastructure.Services.Generation;

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
        services.AddScoped<ILetterheadService, LetterheadService>();
        services.AddScoped<ILetterheadEditorService, LetterheadEditorService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
        services.AddScoped<ITemplateEditorService, TemplateEditorService>();
        services.AddScoped<IResourceEditorService, ResourceEditorService>();
        services.AddScoped<IDomainLogoProvider, DomainLogoProvider>();
        services.AddScoped<ITemplateLetterheadApplier, TemplateLetterheadApplier>();
        services.AddScoped<ITemplateFooterApplier, TemplateFooterApplier>();
        services.AddScoped<ILetterheadFooterApplier, LetterheadFooterApplier>();
        services.AddScoped<ITemplateBrandingApplier, TemplateBrandingApplier>();
        services.AddScoped<IDocumentRenderService, GotenbergDocumentRenderService>();
        services.AddScoped<DocumentContextLoader>();
        services.AddScoped<DocumentIncrementalAllocator>();
        services.AddScoped<DocumentParameterResolver>();
        services.AddScoped<LetterheadHeaderValueEnricher>();
        services.AddScoped<IDocumentGenerationService, DocumentGenerationService>();
        services.AddSingleton<IWopiSessionStore, InMemoryWopiSessionStore>();

        return services;
    }
}
