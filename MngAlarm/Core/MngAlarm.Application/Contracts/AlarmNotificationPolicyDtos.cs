namespace MngAlarm.Application.Contracts;

public sealed class CreateAlarmNotificationPolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? RuleId { get; set; }
    public int? MinSeverity { get; set; }
    public int? MaxSeverity { get; set; }
    public List<string> Channels { get; set; } = [];
    public List<string> RecipientPersonIds { get; set; } = [];
    public string? EmailTemplateKey { get; set; }
    public string? EmailSubject { get; set; }
    public AlarmNotificationPolicySettingsDto? Settings { get; set; }
    public int? CooldownMinutes { get; set; }
    public bool ExcludeAcknowledgedBy { get; set; }
    public int? Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateAlarmNotificationPolicyRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? EventType { get; set; }
    public string? RuleId { get; set; }
    public int? MinSeverity { get; set; }
    public int? MaxSeverity { get; set; }
    public List<string>? Channels { get; set; }
    public List<string>? RecipientPersonIds { get; set; }
    public string? EmailTemplateKey { get; set; }
    public string? EmailSubject { get; set; }
    public AlarmNotificationPolicySettingsDto? Settings { get; set; }
    public int? CooldownMinutes { get; set; }
    public bool? ExcludeAcknowledgedBy { get; set; }
    public int? Priority { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class AlarmNotificationPolicySettingsDto
{
    public bool? PushToast { get; set; }
    public string? ToastSeverity { get; set; }
}
