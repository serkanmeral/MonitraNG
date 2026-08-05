using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Infrastructure.Services;

public sealed class UnavailableScenarioQueryProvider : IScenarioQueryProvider
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<ObservationEnvelope>> QueryAsync(
        ScenarioQueryRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No scheduled-query history provider is configured.");
}

public sealed class ScenarioRuntimeCapabilities(IScenarioQueryProvider queryProvider) : IScenarioRuntimeCapabilities
{
    public bool ScheduledQueryAvailable => queryProvider.IsAvailable;
    public bool MetaCorrelationAvailable => true;
}

public sealed class ScenarioPackageImportAuthorizer(IConfiguration configuration) : IScenarioPackageImportAuthorizer
{
    public bool IsAuthorized(string? suppliedKey)
    {
        var expected = configuration["ScenarioStudio:PackageImportKey"];
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(suppliedKey))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(suppliedKey));
    }
}

public sealed class ScenarioSchedulerService(
    IAlarmDomainAccessor domain,
    IScenarioRepository scenarios,
    IScenarioQueryProvider queryProvider,
    IObservationProcessor processor) : IScenarioSchedulerService
{
    public async Task<ScenarioScheduleTriggerResult> TriggerAsync(
        string scenarioId,
        int version,
        ScenarioScheduleTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var scenario = await scenarios.GetVersionAsync(ctx.DomainName, scenarioId, version, cancellationToken);
        if (scenario == null
            || scenario.Status != ScenarioLifecycleStatuses.Published
            || !scenario.Enabled
            || scenario.Definition.Source.Kind != ScenarioSourceKinds.ScheduledQuery)
        {
            return new ScenarioScheduleTriggerResult { Supported = false, DiagnosticCode = "scheduled.scenario.unavailable" };
        }

        if (!queryProvider.IsAvailable)
            return new ScenarioScheduleTriggerResult { Supported = false, DiagnosticCode = "scheduled.provider.unavailable" };

        var observations = await queryProvider.QueryAsync(
            new ScenarioQueryRequest(
                ctx.DomainId,
                ctx.DomainName,
                scenario,
                request.EvaluationTime ?? DateTime.UtcNow,
                request.Samples),
            cancellationToken);

        var raised = 0;
        foreach (var observation in observations)
        {
            if (!string.Equals(observation.DomainName, ctx.DomainName, StringComparison.Ordinal))
                throw new InvalidOperationException("Scheduled query provider returned an observation for another tenant.");
            raised += (await processor.ProcessAsync(observation, cancellationToken)).AlarmsRaised;
        }

        return new ScenarioScheduleTriggerResult
        {
            Supported = true,
            ObservationsProcessed = observations.Count,
            AlarmsRaised = raised
        };
    }
}
