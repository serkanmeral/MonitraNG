using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Notifications;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public class NotificationOrchestratorService : INotificationOrchestrator
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IMngNotifiersClient _notifiers;
    private readonly IMetadataCache _metadataCache;
    private readonly MngNotifiersSettings _notifierSettings;
    private readonly ILogger<NotificationOrchestratorService> _logger;

    public NotificationOrchestratorService(
        IMngDataGatewayClient dg,
        IMngNotifiersClient notifiers,
        IMetadataCache metadataCache,
        IOptions<MngOperationsSettings> settings,
        ILogger<NotificationOrchestratorService> logger)
    {
        _dg = dg;
        _notifiers = notifiers;
        _metadataCache = metadataCache;
        _notifierSettings = settings.Value.MngNotifiers;
        _logger = logger;
    }

    public async Task DispatchWorkItemEventAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var policies = await _metadataCache.GetNotificationPoliciesForWorkspaceAsync(
                request.WorkspaceId,
                request.Token,
                cancellationToken);

            var matching = policies
                .Where(p => PolicyMatches(p, request))
                .OrderByDescending(p => PolicyScore(p))
                .ThenByDescending(p => p.Priority ?? 0)
                .ToList();

            if (matching.Count == 0)
                return;

            var (title, message, subject, htmlBody) = NotificationMessageBuilder.Build(
                request.EventType,
                request.WorkItemKey,
                WorkItemDataHelper.GetString(request.WorkItem, "title"),
                request.TransitionKey,
                request.FromStateId,
                request.ToStateId);

            foreach (var policy in matching)
            {
                var recipientRoles = ParseStringList(policy.Recipients);
                if (recipientRoles.Count == 0)
                    recipientRoles = new List<string> { "assignee" };

                var userIds = NotificationRecipientResolver.Resolve(
                    request.WorkItem,
                    recipientRoles,
                    request.Actor,
                    policy.ExcludeActor == true);

                var channels = ParseStringList(policy.Channels);
                if (channels.Count == 0)
                    channels = new List<string> { "inApp" };

                foreach (var channel in channels)
                {
                    if (channel.Equals("inApp", StringComparison.OrdinalIgnoreCase))
                    {
                        await CreateInAppNotificationsAsync(
                            userIds,
                            request,
                            policy.NotificationTemplateKey ?? request.EventType,
                            title,
                            message,
                            cancellationToken);
                    }
                    else if (channel.Equals("email", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendEmailAsync(
                            userIds,
                            subject,
                            htmlBody,
                            policy.EmailTemplateKey,
                            cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Notification dispatch failed for {EventType} work item {WorkItemKey} (non-fatal)",
                request.EventType,
                request.WorkItemKey);
        }
    }

    public async Task DispatchRuleSideEffectAsync(
        string effectType,
        IReadOnlyDictionary<string, object?> payload,
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recipientRoles = ParseRecipientPayload(payload.TryGetValue("recipients", out var r) ? r : null);
            if (recipientRoles.Count == 0)
                recipientRoles = new List<string> { "assignee" };

            var userIds = NotificationRecipientResolver.Resolve(
                request.WorkItem,
                recipientRoles,
                request.Actor,
                excludeActor: true);

            var (title, message, subject, htmlBody) = NotificationMessageBuilder.Build(
                request.EventType,
                request.WorkItemKey,
                WorkItemDataHelper.GetString(request.WorkItem, "title"),
                request.TransitionKey,
                request.FromStateId,
                request.ToStateId);

            var templateKey = payload.TryGetValue("templateKey", out var tk) ? tk?.ToString() : null;

            if (effectType.Equals("createNotification", StringComparison.OrdinalIgnoreCase))
            {
                await CreateInAppNotificationsAsync(
                    userIds,
                    request,
                    templateKey ?? request.EventType,
                    title,
                    message,
                    cancellationToken);
            }
            else if (effectType.Equals("sendEmailViaMngNotifiers", StringComparison.OrdinalIgnoreCase))
            {
                await SendEmailAsync(userIds, subject, htmlBody, templateKey, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rule notification side-effect failed for {WorkItemKey} (non-fatal)", request.WorkItemKey);
        }
    }

    public async Task DispatchMentionAsync(
        string workItemId,
        string workItemKey,
        IReadOnlyList<string> mentionedUserIds,
        string? actorUserId,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recipients = mentionedUserIds
                .Where(id => !string.IsNullOrWhiteSpace(id)
                    && !string.Equals(id, actorUserId, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0)
                return;

            const string title = "Bir yorumda etiketlendiniz";
            var message = $"{workItemKey} kaydındaki bir yorumda etiketlendiniz.";

            foreach (var userId in recipients)
            {
                try
                {
                    await _dg.CreateAsync(OcDatasets.Notifications, new Dictionary<string, object?>
                    {
                        ["userId"] = userId,
                        ["notificationType"] = "CommentMention",
                        ["title"] = title,
                        ["message"] = message,
                        ["sourceDataset"] = OcDatasets.WorkItems,
                        ["sourceRecordId"] = workItemId,
                        ["workItemId"] = workItemId,
                        ["workItemKey"] = workItemKey,
                        ["isRead"] = false,
                        ["createdAt"] = DateTime.UtcNow.ToString("o")
                    }, token, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mention notification create failed for user {UserId}", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mention dispatch failed for {WorkItemKey} (non-fatal)", workItemKey);
        }
    }

    public async Task DispatchAssignmentAsync(
        string workItemId,
        string workItemKey,
        string? assigneeId,
        string? previousAssigneeId,
        string? actorUserId,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(assigneeId))
                return;

            // Atama değişmediyse veya kişi atamayı kendisine yaptıysa bildirim yok.
            if (string.Equals(assigneeId, previousAssigneeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(assigneeId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            const string title = "Bir iş kaydı size atandı";
            var message = $"{workItemKey} kaydı size atandı.";

            await _dg.CreateAsync(OcDatasets.Notifications, new Dictionary<string, object?>
            {
                ["userId"] = assigneeId.Trim(),
                ["notificationType"] = "WorkItemAssigned",
                ["title"] = title,
                ["message"] = message,
                ["sourceDataset"] = OcDatasets.WorkItems,
                ["sourceRecordId"] = workItemId,
                ["workItemId"] = workItemId,
                ["workItemKey"] = workItemKey,
                ["isRead"] = false,
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            }, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Assignment dispatch failed for {WorkItemKey} (non-fatal)", workItemKey);
        }
    }

    private async Task CreateInAppNotificationsAsync(
        IReadOnlyList<string> userIds,
        NotificationDispatchRequest request,
        string notificationType,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            try
            {
                await _dg.CreateAsync(OcDatasets.Notifications, new Dictionary<string, object?>
                {
                    ["userId"] = userId,
                    ["notificationType"] = notificationType,
                    ["title"] = title,
                    ["message"] = message,
                    ["sourceDataset"] = OcDatasets.WorkItems,
                    ["sourceRecordId"] = request.WorkItemId,
                    ["workItemId"] = request.WorkItemId,
                    ["workItemKey"] = request.WorkItemKey,
                    ["isRead"] = false,
                    ["createdAt"] = DateTime.UtcNow.ToString("o")
                }, request.Token, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "In-app notification create failed for user {UserId}", userId);
            }
        }
    }

    private async Task SendEmailAsync(
        IReadOnlyList<string> recipients,
        string subject,
        string htmlBody,
        string? templateKey,
        CancellationToken cancellationToken)
    {
        var emails = NotificationRecipientResolver.ToEmailAddresses(recipients, _notifierSettings.EmailDomainSuffix);
        if (emails.Count == 0)
        {
            _logger.LogDebug(
                "No email addresses resolved for notification (templateKey={TemplateKey})",
                templateKey);
            return;
        }

        var result = await _notifiers.SendMailAsync(new SendMailRequest
        {
            To = emails.ToList(),
            Subject = subject,
            Body = htmlBody,
            IsHtml = true
        }, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Email notification failed (templateKey={TemplateKey}): {Error}",
                templateKey,
                result.ErrorMessage);
        }
    }

    private static bool PolicyMatches(NotificationPolicyRecord policy, NotificationDispatchRequest request)
    {
        if (policy.IsActive == false)
            return false;

        if (!string.Equals(policy.EventType, request.EventType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(policy.TypeId)
            && !string.Equals(policy.TypeId, request.TypeId ?? WorkItemDataHelper.GetString(request.WorkItem, "typeId"), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(policy.BoardId)
            && !string.Equals(policy.BoardId, request.BoardId ?? WorkItemDataHelper.GetString(request.WorkItem, "boardId"), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static int PolicyScore(NotificationPolicyRecord policy)
    {
        var score = 0;
        if (!string.IsNullOrEmpty(policy.TypeId))
            score += 2;
        if (!string.IsNullOrEmpty(policy.BoardId))
            score += 1;
        return score;
    }

    private static IReadOnlyList<string> ParseStringList(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array })
            return Array.Empty<string>();

        return element.Value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();
    }

    private static IReadOnlyList<string> ParseRecipientPayload(object? value)
    {
        if (value is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            return el.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }

        if (value is IEnumerable<object?> list && value is not string)
        {
            return list
                .Select(v => v?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }

        if (value is string s && !string.IsNullOrWhiteSpace(s))
            return new[] { s };

        return Array.Empty<string>();
    }
}
