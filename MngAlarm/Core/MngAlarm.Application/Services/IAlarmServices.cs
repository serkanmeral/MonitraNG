using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;

namespace MngAlarm.Application.Services;

public interface IAlarmDomainAccessor
{
    AlarmDomainContext GetRequiredDomain();
}

public interface IAlarmRuleRepository
{
    Task InsertAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default);
    Task<AlarmRuleDocument?> GetByIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default);
    Task UpdateAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(string domainName, string ruleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByKeyAsync(string domainName, string matchKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByTypeAsync(string domainName, string type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlarmRuleDocument>> ListAllAsync(string domainName, CancellationToken cancellationToken = default);
}

public interface IAlarmRepository
{
    Task<AlarmDocument?> GetActiveByDedupKeyAsync(string domainName, string dedupKey, CancellationToken cancellationToken = default);
    Task<AlarmDocument?> GetByIdAsync(string domainName, string alarmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlarmDocument>> ListActiveByRuleIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AlarmDocument> Items, long Total)> ListAsync(
        string domainName,
        AlarmStatus? status,
        int? minSeverity,
        bool openOnly,
        int skip,
        int limit,
        string? ruleId = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
    Task InsertAsync(AlarmDocument alarm, CancellationToken cancellationToken = default);
    Task UpdateAsync(AlarmDocument alarm, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlarmScenarioRollupDto>> GetScenarioRollupAsync(
        string domainName,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}

public interface IAlarmEventPublisher
{
    Task PublishAsync(AlarmEventMessage message, string lifecycle, CancellationToken cancellationToken = default);
}

public interface IAlarmRuleService
{
    Task<AlarmRuleDocument> CreateAsync(CreateAlarmRuleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlarmRuleDocument>> ListAsync(CancellationToken cancellationToken = default);
    Task<AlarmRuleDocument?> GetAsync(string ruleId, CancellationToken cancellationToken = default);
    Task<AlarmRuleDocument?> UpdateAsync(string ruleId, UpdateAlarmRuleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string ruleId, CancellationToken cancellationToken = default);
}

public interface IAlarmNotificationPolicyRepository
{
    Task InsertAsync(AlarmNotificationPolicyDocument policy, CancellationToken cancellationToken = default);
    Task<AlarmNotificationPolicyDocument?> GetByIdAsync(string domainName, string policyId, CancellationToken cancellationToken = default);
    Task UpdateAsync(AlarmNotificationPolicyDocument policy, CancellationToken cancellationToken = default);
    Task DeleteAsync(string domainName, string policyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlarmNotificationPolicyDocument>> ListAsync(
        string domainName,
        bool? isActive,
        CancellationToken cancellationToken = default);
}

public interface IAlarmNotificationDispatchService
{
    Task DispatchAsync(AlarmEventMessage message, CancellationToken cancellationToken = default);
}

public interface IAlarmNotificationPolicyService
{
    Task<AlarmNotificationPolicyDocument> CreateAsync(
        CreateAlarmNotificationPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlarmNotificationPolicyDocument>> ListAsync(
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<AlarmNotificationPolicyDocument?> GetAsync(string policyId, CancellationToken cancellationToken = default);

    Task<AlarmNotificationPolicyDocument?> UpdateAsync(
        string policyId,
        UpdateAlarmNotificationPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string policyId, CancellationToken cancellationToken = default);
}

public interface IAlarmQueryService
{
    Task<AlarmListResponse> ListAsync(
        AlarmStatus? status,
        int? minSeverity,
        bool openOnly,
        int skip,
        int limit,
        string? ruleId = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    Task<AlarmSummaryDto?> GetAsync(string alarmId, CancellationToken cancellationToken = default);

    Task<AlarmDashboardSnapshot> GetDashboardSnapshotAsync(
        int rangeHours = 24,
        int minSeverity = 6,
        int openLimit = 15,
        CancellationToken cancellationToken = default);
}

public interface IObservationProcessor
{
    Task<AlarmProcessResult> ProcessAsync(ObservationEnvelope observation, CancellationToken cancellationToken = default);
}

public interface IAlarmValidationService
{
    Task<AlarmValidationScanResponse> RunScanAsync(string domainName, string domainId, CancellationToken cancellationToken = default);
}
