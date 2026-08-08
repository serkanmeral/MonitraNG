using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Infrastructure.Services;

public sealed class ScenarioDueEvaluationService(
    IServiceScopeFactory scopeFactory,
    IScenarioDueStateStore dueStates,
    TimeProvider timeProvider,
    IOptions<MngAlarmSettings> settings,
    ILogger<ScenarioDueEvaluationService> logger) : BackgroundService
{
    private readonly ScenarioDueEvaluationSettings _settings = settings.Value.Engine.ScenarioDueEvaluation;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "V3 due-state scan failed.");
            }
            await Task.Delay(
                TimeSpan.FromSeconds(Math.Clamp(_settings.ScanIntervalSeconds, 1, 300)),
                timeProvider,
                stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claimed = await dueStates.ClaimDueAsync(
            now,
            TimeSpan.FromSeconds(Math.Clamp(_settings.ClaimLeaseSeconds, 5, 300)),
            Math.Clamp(_settings.BatchSize, 1, 500),
            cancellationToken);
        var completed = 0;
        foreach (var state in claimed)
        {
            try
            {
                if (!await dueStates.IsClaimValidAsync(state.Id, state.ClaimToken!, cancellationToken))
                    continue;
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IObservationProcessor>();
                await processor.ProcessDueAsync(state, cancellationToken);
                if (await dueStates.CompleteAsync(state.Id, state.ClaimToken!, cancellationToken))
                    completed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "V3 due-state evaluation failed state={StateId} rule={RuleId} node={NodeId}",
                    state.Id,
                    state.RuleId,
                    state.NodeId);
                try
                {
                    using var errorScope = scopeFactory.CreateScope();
                    var rules = errorScope.ServiceProvider.GetRequiredService<IAlarmRuleRepository>();
                    var rule = await rules.GetByIdAsync(state.DomainName, state.RuleId, cancellationToken);
                    if (rule != null && !string.IsNullOrWhiteSpace(rule.ScenarioId))
                    {
                        rule.RuntimeHealth ??= new ScenarioRuntimeHealth();
                        ScenarioHealthTracker.RecordError(
                            rule.RuntimeHealth,
                            timeProvider.GetUtcNow().UtcDateTime,
                            ex,
                            state.NodeId);
                        rule.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                        await rules.UpdateAsync(rule, cancellationToken);
                    }
                }
                catch (Exception healthEx)
                {
                    logger.LogWarning(healthEx, "Failed to persist runtime health for rule={RuleId}", state.RuleId);
                }

                await dueStates.ReleaseAsync(
                    state.Id,
                    state.ClaimToken!,
                    now.AddSeconds(Math.Clamp(_settings.RetryDelaySeconds, 1, 300)),
                    cancellationToken);
            }
        }
        return completed;
    }
}
