using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public sealed class InAppNotificationComposer : IInAppNotificationComposer
{
    private const string TemplatesDataset = "@notification_templates";

    private static readonly Dictionary<string, string> DefaultTemplateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WorkItemCreated"] = "work-item-created-inapp",
        ["WorkItemTransitioned"] = "work-item-transitioned-inapp",
        ["WorkItemUpdated"] = "work-item-updated-inapp",
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IMetadataCache _metadataCache;
    private readonly IKeeperDirectoryClient _keeper;
    private readonly ILogger<InAppNotificationComposer> _logger;

    public InAppNotificationComposer(
        IMngDataGatewayClient dg,
        IMetadataCache metadataCache,
        IKeeperDirectoryClient keeper,
        ILogger<InAppNotificationComposer> logger)
    {
        _dg = dg;
        _metadataCache = metadataCache;
        _keeper = keeper;
        _logger = logger;
    }

    public async Task<InAppNotificationContent> ComposeAsync(
        NotificationDispatchRequest request,
        NotificationPolicyRecord? policy,
        string? templateKeyOverride,
        CancellationToken cancellationToken = default)
    {
        var notificationType = templateKeyOverride
            ?? policy?.NotificationTemplateKey
            ?? request.EventType;

        var templateKey = ResolveTemplateKey(request.EventType, policy?.NotificationTemplateKey);
        var context = await MailNotificationContextBuilder.BuildAsync(
            request,
            _metadataCache,
            _keeper,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(templateKey))
        {
            var rendered = await TryRenderTemplateAsync(templateKey, context, request.Token, cancellationToken);
            if (rendered != null)
            {
                return new InAppNotificationContent
                {
                    Title = rendered.Value.Title,
                    Message = rendered.Value.Message,
                    ToastSeverity = ResolveToastSeverity(policy, rendered.Value.DefaultSeverity),
                    NotificationType = notificationType
                };
            }
        }

        var (title, message) = await InAppNotificationMessageBuilder.BuildAsync(
            request,
            _metadataCache,
            request.Token,
            cancellationToken);

        return new InAppNotificationContent
        {
            Title = title,
            Message = message,
            ToastSeverity = ResolveToastSeverity(policy, null),
            NotificationType = notificationType
        };
    }

    private static string? ResolveTemplateKey(string eventType, string? policyTemplateKey)
    {
        if (!string.IsNullOrWhiteSpace(policyTemplateKey))
            return policyTemplateKey.Trim();

        return DefaultTemplateKeys.TryGetValue(eventType, out var key) ? key : null;
    }

    private async Task<(string Title, string Message, string? DefaultSeverity)?> TryRenderTemplateAsync(
        string templateKey,
        JsonElement context,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var filter = $"templateKey:eq:{templateKey}";
            var rows = await _dg.GetAsync<InAppNotificationTemplateRecord>(
                TemplatesDataset,
                $"filter={Uri.EscapeDataString(filter)}&limit=1",
                token,
                cancellationToken);

            var template = rows.FirstOrDefault();
            if (template == null || template.IsActive == false)
                return null;

            var titleTemplate = template.Title?.Trim();
            var messageTemplate = template.Message?.Trim();
            if (string.IsNullOrEmpty(titleTemplate) && string.IsNullOrEmpty(messageTemplate))
                return null;

            var title = string.IsNullOrEmpty(titleTemplate)
                ? string.Empty
                : TextTemplateRenderer.Render(titleTemplate, context);
            var message = string.IsNullOrEmpty(messageTemplate)
                ? string.Empty
                : TextTemplateRenderer.Render(messageTemplate, context);

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(message))
                return null;

            return (title, message, template.DefaultToastSeverity?.Trim().ToLowerInvariant());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "In-app template render failed for {TemplateKey} (fallback)", templateKey);
            return null;
        }
    }

    private static string? ResolveToastSeverity(NotificationPolicyRecord? policy, string? templateDefault)
    {
        if (policy?.Settings is { ValueKind: JsonValueKind.Object } settings
            && settings.TryGetProperty("toastSeverity", out var sev)
            && sev.ValueKind == JsonValueKind.String)
        {
            var raw = sev.GetString()?.Trim().ToLowerInvariant();
            if (IsValidSeverity(raw))
                return raw;
        }

        if (!string.IsNullOrWhiteSpace(templateDefault) && IsValidSeverity(templateDefault))
            return templateDefault;

        return "info";
    }

    private static bool IsValidSeverity(string? value) =>
        value is "info" or "success" or "warning" or "error";
}
