using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Infrastructure.Services;

public sealed class AlarmNotificationPolicyService(
    IAlarmDomainAccessor domain,
    IAlarmNotificationPolicyRepository policies) : IAlarmNotificationPolicyService
{
    private static readonly HashSet<string> ValidEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        AlarmNotificationEventTypes.Raised,
        AlarmNotificationEventTypes.Updated,
        AlarmNotificationEventTypes.Resolved,
    };

    public async Task<AlarmNotificationPolicyDocument> CreateAsync(
        CreateAlarmNotificationPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreate(request);

        var ctx = domain.GetRequiredDomain();
        var now = DateTime.UtcNow;
        var policy = new AlarmNotificationPolicyDocument
        {
            DomainId = ctx.DomainId,
            DomainName = ctx.DomainName,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            EventType = request.EventType.Trim(),
            RuleId = string.IsNullOrWhiteSpace(request.RuleId) ? null : request.RuleId.Trim(),
            MinSeverity = request.MinSeverity,
            MaxSeverity = request.MaxSeverity,
            Channels = NormalizeChannels(request.Channels),
            RecipientPersonIds = NormalizeRecipients(request.RecipientPersonIds),
            EmailTemplateKey = string.IsNullOrWhiteSpace(request.EmailTemplateKey) ? null : request.EmailTemplateKey.Trim(),
            EmailSubject = string.IsNullOrWhiteSpace(request.EmailSubject) ? null : request.EmailSubject.Trim(),
            Settings = MapSettings(request.Settings),
            CooldownMinutes = request.CooldownMinutes,
            ExcludeAcknowledgedBy = request.ExcludeAcknowledgedBy,
            Priority = request.Priority,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await policies.InsertAsync(policy, cancellationToken);
        return policy;
    }

    public Task<IReadOnlyList<AlarmNotificationPolicyDocument>> ListAsync(
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        return policies.ListAsync(ctx.DomainName, isActive, cancellationToken);
    }

    public async Task<AlarmNotificationPolicyDocument?> GetAsync(string policyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyId))
            return null;

        var ctx = domain.GetRequiredDomain();
        return await policies.GetByIdAsync(ctx.DomainName, policyId.Trim(), cancellationToken);
    }

    public async Task<AlarmNotificationPolicyDocument?> UpdateAsync(
        string policyId,
        UpdateAlarmNotificationPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var existing = await policies.GetByIdAsync(ctx.DomainName, policyId.Trim(), cancellationToken);
        if (existing == null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            existing.Name = request.Name.Trim();
        if (request.Description != null)
            existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            var eventType = request.EventType.Trim();
            if (!ValidEventTypes.Contains(eventType))
                throw new ArgumentException($"Invalid eventType: {eventType}");
            existing.EventType = eventType;
        }
        if (request.RuleId != null)
            existing.RuleId = string.IsNullOrWhiteSpace(request.RuleId) ? null : request.RuleId.Trim();
        if (request.MinSeverity.HasValue)
            existing.MinSeverity = request.MinSeverity;
        if (request.MaxSeverity.HasValue)
            existing.MaxSeverity = request.MaxSeverity;
        if (request.Channels != null)
            existing.Channels = NormalizeChannels(request.Channels);
        if (request.RecipientPersonIds != null)
            existing.RecipientPersonIds = NormalizeRecipients(request.RecipientPersonIds);
        if (request.EmailTemplateKey != null)
            existing.EmailTemplateKey = string.IsNullOrWhiteSpace(request.EmailTemplateKey) ? null : request.EmailTemplateKey.Trim();
        if (request.EmailSubject != null)
            existing.EmailSubject = string.IsNullOrWhiteSpace(request.EmailSubject) ? null : request.EmailSubject.Trim();
        if (request.Settings != null)
            existing.Settings = MapSettings(request.Settings);
        if (request.CooldownMinutes.HasValue)
            existing.CooldownMinutes = request.CooldownMinutes;
        if (request.ExcludeAcknowledgedBy.HasValue)
            existing.ExcludeAcknowledgedBy = request.ExcludeAcknowledgedBy.Value;
        if (request.Priority.HasValue)
            existing.Priority = request.Priority;
        if (request.IsActive.HasValue)
            existing.IsActive = request.IsActive.Value;

        ValidatePolicy(existing);
        existing.UpdatedAt = DateTime.UtcNow;
        await policies.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(string policyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyId))
            return false;

        var ctx = domain.GetRequiredDomain();
        var existing = await policies.GetByIdAsync(ctx.DomainName, policyId.Trim(), cancellationToken);
        if (existing == null)
            return false;

        await policies.DeleteAsync(ctx.DomainName, existing.Id, cancellationToken);
        return true;
    }

    private static void ValidateCreate(CreateAlarmNotificationPolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("name is required");
        if (string.IsNullOrWhiteSpace(request.EventType) || !ValidEventTypes.Contains(request.EventType.Trim()))
            throw new ArgumentException("eventType is required (AlarmRaised, AlarmUpdated, AlarmResolved)");

        var draft = new AlarmNotificationPolicyDocument
        {
            Channels = NormalizeChannels(request.Channels),
            RecipientPersonIds = NormalizeRecipients(request.RecipientPersonIds),
            EmailTemplateKey = request.EmailTemplateKey,
        };
        ValidatePolicy(draft);
    }

    private static void ValidatePolicy(AlarmNotificationPolicyDocument policy)
    {
        if (policy.Channels.Count == 0)
            throw new ArgumentException("channels is required");
        if (policy.RecipientPersonIds.Count == 0)
            throw new ArgumentException("recipientPersonIds is required");
        if (policy.Channels.Any(c => c.Equals(AlarmNotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(policy.EmailTemplateKey))
        {
            throw new ArgumentException("emailTemplateKey is required when email channel is enabled");
        }
    }

    private static List<string> NormalizeChannels(IEnumerable<string>? channels) =>
        (channels ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> NormalizeRecipients(IEnumerable<string>? recipients) =>
        (recipients ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static AlarmNotificationPolicySettings? MapSettings(AlarmNotificationPolicySettingsDto? settings)
    {
        if (settings == null)
            return null;

        return new AlarmNotificationPolicySettings
        {
            PushToast = settings.PushToast,
            ToastSeverity = string.IsNullOrWhiteSpace(settings.ToastSeverity) ? null : settings.ToastSeverity.Trim().ToLowerInvariant(),
        };
    }
}
