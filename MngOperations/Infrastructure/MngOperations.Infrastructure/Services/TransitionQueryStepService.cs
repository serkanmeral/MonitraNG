using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Before conditions, runs named dataset queries configured on matching rules
/// and writes the first row onto the work item. Parameter values come from the work item.
/// </summary>
public sealed class TransitionQueryStepService
{
    private readonly IMetadataCache _metadataCache;
    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<TransitionQueryStepService> _logger;

    public TransitionQueryStepService(
        IMetadataCache metadataCache,
        IMngDataGatewayClient dg,
        IRequestContext requestContext,
        ILogger<TransitionQueryStepService> logger)
    {
        _metadataCache = metadataCache;
        _dg = dg;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task ApplyAsync(
        Dictionary<string, object?> workItem,
        RuleExecutionContext context,
        WorkspaceRecord workspace,
        string token,
        CancellationToken cancellationToken)
    {
        var rules = await _metadataCache.GetRulesForWorkspaceAsync(context.WorkspaceId, token, cancellationToken);
        var utcNow = DateTime.UtcNow;
        var matched = rules
            .Where(rule => RuleScopeMatcher.MatchesTrigger(rule, context.Trigger))
            .Where(rule => RuleScopeMatcher.Matches(rule, context, utcNow))
            .Where(rule => rule.QuerySteps is { ValueKind: JsonValueKind.Array } && rule.QuerySteps.Value.GetArrayLength() > 0)
            .OrderByDescending(rule => rule.Priority ?? 0)
            .ThenBy(rule => rule.Name ?? rule.DataId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matched.Count == 0)
            return;

        var catalog = await WorkItemFieldCatalog.BuildEnabledPoolFieldsByKeyAsync(
            workspace, _metadataCache, token, cancellationToken);

        foreach (var rule in matched)
        {
            foreach (var step in rule.QuerySteps!.Value.EnumerateArray())
            {
                var dataset = TransitionQueryStepApplier.ReadString(step, "dataset");
                var query = TransitionQueryStepApplier.ReadString(step, "query");
                if (string.IsNullOrWhiteSpace(dataset) || string.IsNullOrWhiteSpace(query))
                    continue;

                var parameters = TransitionQueryStepApplier.ResolveParameters(step, workItem, ActorValues());
                IReadOnlyList<Dictionary<string, object?>> rows;
                try
                {
                    rows = await _dg.ExecuteQueryAsync(dataset, query, parameters, token, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Transition query {Dataset}/{Query} failed for rule {Rule}", dataset, query, rule.Name);
                    throw new OperationCoreException(
                        "QUERY_FAILED",
                        $"Query '{query}' on '{dataset}' failed.",
                        $"'{dataset}' sorgusu '{query}' çalışmadı.",
                        400);
                }

                var mutations = TransitionQueryStepApplier.BuildMutations(step, rows);
                if (mutations.Count == 0)
                    continue;

                WorkItemFieldWriter.Apply(workItem, mutations, catalog);
            }
        }
    }

    private Dictionary<string, object?> ActorValues()
    {
        var groups = _requestContext.UserGroups ?? Array.Empty<string>();
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["personId"] = _requestContext.MngPersonId,
            ["groups"] = string.Join(",", groups),
        };
    }
}
