using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;
using MngAlarm.Application.Notifications;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Clients;
using MngAlarm.Infrastructure.Persistence.Repositories;

namespace MngAlarm.Infrastructure.Services;

public sealed class AlarmNotificationDispatchService(
    IOptions<MngAlarmSettings> settings,
    IAlarmNotificationPolicyRepository policies,
    IAlarmRuleRepository rules,
    IAlarmNotificationCooldownStore cooldowns,
    IAlarmDispatchTokenProvider tokenProvider,
    IAlarmOpNotificationsClient opNotifications,
    IAlarmHubNotificationClient hub,
    IAlarmNotifiersDispatchClient notifiers,
    IAlarmKeeperUsersClient keeperUsers,
    ILogger<AlarmNotificationDispatchService> logger) : IAlarmNotificationDispatchService
{
    public async Task DispatchAsync(AlarmEventMessage message, CancellationToken cancellationToken = default)
    {
        var dispatch = settings.Value.NotificationDispatch;
        if (!dispatch.Enabled)
            return;

        try
        {
            var token = await tokenProvider.GetServiceTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogWarning("Alarm notification dispatch skipped: no service token");
                return;
            }

            var policyRows = await policies.ListAsync(message.DomainName, isActive: true, cancellationToken);
            var matching = policyRows
                .Where(p => AlarmNotificationPolicyMatcher.Matches(p, message))
                .OrderByDescending(p => AlarmNotificationPolicyMatcher.SpecificityScore(p))
                .ThenByDescending(p => p.Priority ?? 0)
                .ToList();

            if (matching.Count == 0)
                return;

            var rule = await rules.GetByIdAsync(message.DomainName, message.RuleId, cancellationToken);
            var ruleName = string.IsNullOrWhiteSpace(rule?.Name) ? message.RuleId : rule!.Name.Trim();
            var deepLink = $"/apps/alarm-center/alarms?alarmId={Uri.EscapeDataString(message.AlarmId)}";
            var mailContext = BuildMailContext(message, ruleName);

            foreach (var policy in matching)
            {
                if (!await cooldowns.TryAcquireAsync(
                        message.DomainName,
                        policy.Id,
                        message.AlarmId,
                        policy.CooldownMinutes ?? 0,
                        cancellationToken))
                {
                    logger.LogDebug(
                        "Alarm notification cooldown skip policy={PolicyId} alarm={AlarmId}",
                        policy.Id,
                        message.AlarmId);
                    continue;
                }

                var dispatched = false;
                var (title, body, defaultSeverity) = BuildInAppContent(message, ruleName);

                foreach (var userId in policy.RecipientPersonIds)
                {
                    if (string.IsNullOrWhiteSpace(userId))
                        continue;

                    foreach (var channel in policy.Channels)
                    {
                        if (channel.Equals(AlarmNotificationChannels.InApp, StringComparison.OrdinalIgnoreCase))
                        {
                            await CreateInAppAsync(
                                token,
                                userId.Trim(),
                                message,
                                policy,
                                title,
                                body,
                                defaultSeverity,
                                deepLink,
                                cancellationToken);
                            dispatched = true;
                        }
                    }
                }

                if (policy.Channels.Any(c => c.Equals(AlarmNotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrWhiteSpace(policy.EmailTemplateKey))
                {
                    var recipients = await keeperUsers.ResolveRecipientsAsync(
                        policy.RecipientPersonIds,
                        token,
                        cancellationToken);
                    foreach (var recipient in recipients)
                    {
                        var perRecipientContext = new Dictionary<string, object?>(mailContext, StringComparer.Ordinal)
                        {
                            ["recipient"] = new Dictionary<string, object?>
                            {
                                ["displayName"] = recipient.DisplayName,
                                ["email"] = recipient.Email,
                            },
                        };
                        await notifiers.SendTemplateAsync(
                            token,
                            [recipient.Email],
                            policy.EmailTemplateKey!.Trim(),
                            policy.EmailSubject,
                            perRecipientContext,
                            cancellationToken);
                        dispatched = true;
                    }
                }

                if (dispatched)
                {
                    await cooldowns.MarkDispatchedAsync(
                        message.DomainName,
                        policy.Id,
                        message.AlarmId,
                        cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Alarm notification dispatch failed for {EventType} alarm {AlarmId} (non-fatal)",
                message.EventType,
                message.AlarmId);
        }
    }

    private async Task CreateInAppAsync(
        string token,
        string userId,
        AlarmEventMessage message,
        AlarmNotificationPolicyDocument policy,
        string title,
        string body,
        string defaultSeverity,
        string deepLink,
        CancellationToken cancellationToken)
    {
        var createdAt = DateTime.UtcNow;
        await opNotifications.CreateAsync(token, new Dictionary<string, object?>
        {
            ["userId"] = userId,
            ["notificationType"] = message.EventType,
            ["title"] = title,
            ["message"] = body,
            ["sourceDataset"] = "alarms",
            ["sourceRecordId"] = message.AlarmId,
            ["severity"] = message.Severity,
            ["deepLink"] = deepLink,
            ["isRead"] = false,
            ["createdAt"] = createdAt.ToString("o"),
        }, cancellationToken);

        if (policy.Settings?.PushToast == true)
        {
            var toastSeverity = policy.Settings.ToastSeverity ?? defaultSeverity;
            await hub.PushAsync(
                userId,
                title,
                body,
                message.EventType,
                deepLink,
                toastSeverity,
                cancellationToken);
        }
    }

    private static (string Title, string Message, string ToastSeverity) BuildInAppContent(
        AlarmEventMessage message,
        string ruleName)
    {
        var title = message.EventType switch
        {
            AlarmNotificationEventTypes.Raised => $"Alarm acildi (onem {message.Severity})",
            AlarmNotificationEventTypes.Resolved => $"Alarm kapandi (onem {message.Severity})",
            _ => $"Alarm guncellendi (onem {message.Severity})",
        };

        var body = $"{ruleName} - {message.AlarmId}";
        var severity = message.Severity >= 8 ? "error" : message.Severity >= 5 ? "warning" : "info";
        return (title, body, severity);
    }

    private static Dictionary<string, object?> BuildMailContext(AlarmEventMessage message, string ruleName) =>
        new(StringComparer.Ordinal)
        {
            ["alarm"] = new Dictionary<string, object?>
            {
                ["id"] = message.AlarmId,
                ["severity"] = message.Severity,
            },
            ["rule"] = new Dictionary<string, object?>
            {
                ["id"] = message.RuleId,
                ["name"] = ruleName,
            },
            ["event"] = new Dictionary<string, object?>
            {
                ["type"] = message.EventType,
                ["timestamp"] = message.OccurredAt.ToString("o"),
            },
            ["domain"] = new Dictionary<string, object?>
            {
                ["name"] = message.DomainName,
            },
        };
}
