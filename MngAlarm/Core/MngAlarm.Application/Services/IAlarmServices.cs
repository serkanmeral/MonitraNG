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
    async Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledV3CandidatesAsync(
        string domainName,
        string matchKey,
        CancellationToken cancellationToken = default) =>
        (await ListAllAsync(domainName, cancellationToken))
            .Where(x => x.Enabled
                && x.Definition?.SchemaVersion == 3
                && x.Definition.Graph?.Nodes.Any(n =>
                    n.Type == ScenarioNodeTypes.Source
                    && n.Config.Source != null
                    && (n.Config.Source.MatchKey == matchKey
                        || (n.Config.Source.MatchKeys?.Contains(matchKey) == true))) == true)
            .ToList();
}

public interface IScenarioRepository
{
    Task InsertVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default);
    Task UpdateVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> GetVersionAsync(string domainName, string scenarioId, int version, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> GetLatestAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> GetPublishedAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioVersionDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioVersionDocument>> ListVersionsAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default);
    Task ArchiveVersionAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task ArchivePublishedExceptAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task InsertAuditAsync(ScenarioAuditDocument audit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioAuditDocument>> ListAuditAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default);
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

    Task<IReadOnlyList<AlarmTrendBucketDto>> GetTrendBucketsAsync(
        string domainName,
        DateTime from,
        DateTime to,
        IReadOnlyList<DateTime> hourStarts,
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

public interface IScenarioService
{
    Task<IReadOnlyList<ScenarioCatalogItem>> ListAsync(bool includeDrafts, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument> CreateDraftAsync(CreateScenarioDraftRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> CreateNextDraftAsync(string scenarioId, CreateScenarioDraftRequest? request, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> CloneTemplateAsync(string scenarioId, int version, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> UpdateDraftAsync(string scenarioId, int version, UpdateScenarioDraftRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> GetAsync(string scenarioId, int? version, CancellationToken cancellationToken = default);
    Task<ScenarioValidationSnapshot?> ValidateAsync(string scenarioId, int version, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> PublishAsync(string scenarioId, int version, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> ArchiveAsync(string scenarioId, int version, CancellationToken cancellationToken = default);
    Task<ScenarioVersionDocument?> RollbackAsync(string scenarioId, int version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioAuditDocument>> AuditAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<ScenarioPreviewResponse> CompileAsync(string? scenarioId, int? version, ScenarioPreviewRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioPreviewResponse> PreviewAsync(string? scenarioId, int? version, ScenarioPreviewRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioPackageImportResult> ImportProductPackageAsync(ImportScenarioPackageRequest request, CancellationToken cancellationToken = default);
}

public interface IScenarioRuntimeCapabilities
{
    bool ScheduledQueryAvailable { get; }
    bool MetaCorrelationAvailable { get; }
}

public sealed record ScenarioQueryRequest(
    string DomainId,
    string DomainName,
    ScenarioVersionDocument Scenario,
    DateTime EvaluationTime,
    IReadOnlyList<ScenarioSampleObservation>? SuppliedSamples);

public interface IScenarioQueryProvider
{
    bool IsAvailable { get; }
    Task<IReadOnlyList<ObservationEnvelope>> QueryAsync(ScenarioQueryRequest request, CancellationToken cancellationToken = default);
}

public interface IScenarioSchedulerService
{
    Task<ScenarioScheduleTriggerResult> TriggerAsync(
        string scenarioId,
        int version,
        ScenarioScheduleTriggerRequest request,
        CancellationToken cancellationToken = default);
}

public interface IScenarioPackageImportAuthorizer
{
    bool IsAuthorized(string? suppliedKey);
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

    Task<AlarmTrendBucketsResult> GetTrendBucketsAsync(
        int rangeHours = 24,
        CancellationToken cancellationToken = default);
}

public interface IObservationProcessor
{
    Task<AlarmProcessResult> ProcessAsync(ObservationEnvelope observation, CancellationToken cancellationToken = default);
    Task<AlarmProcessResult> ProcessDueAsync(
        ScenarioDueStateDocument state,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AlarmProcessResult());
}

public interface IAlarmValidationService
{
    Task<AlarmValidationScanResponse> RunScanAsync(string domainName, string domainId, CancellationToken cancellationToken = default);
}
