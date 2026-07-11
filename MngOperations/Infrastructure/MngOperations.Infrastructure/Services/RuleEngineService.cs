using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Rules;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public sealed class RuleEngineService : IRuleEngine
{
    private readonly IMetadataCache _metadataCache;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<RuleEngineService> _logger;

    private static readonly HashSet<string> ForbiddenMutations = new(StringComparer.OrdinalIgnoreCase)
    {
        "__dataId", "key", "workspaceId", "workspaceKey", "origin"
    };

    public RuleEngineService(
        IMetadataCache metadataCache,
        IRequestContext requestContext,
        ILogger<RuleEngineService> logger)
    {
        _metadataCache = metadataCache;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<RuleExecutionResult> ExecuteAsync(
        RuleExecutionContext context,
        RulePhase phase,
        CancellationToken cancellationToken = default)
    {
        var token = _requestContext.BearerToken
            ?? throw new InvalidOperationException("Bearer token is required for rule execution.");

        var rules = await _metadataCache.GetRulesForWorkspaceAsync(context.WorkspaceId, token, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var matched = rules
            .Where(r => RuleScopeMatcher.MatchesTrigger(r, context.Trigger))
            .Where(r => RuleScopeMatcher.MatchesPhase(r, phase))
            .Where(r => RuleScopeMatcher.Matches(r, context, utcNow))
            .OrderByDescending(r => r.Priority ?? 0)
            .ThenBy(r => r.Name ?? r.DataId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mutations = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<RuleValidationError>();
        var sideEffects = new List<RuleSideEffect>();

        foreach (var rule in matched)
        {
            var workItemView = MergeWorkItem(context.WorkItem, mutations);

            if (!RuleConditionEvaluator.Evaluate(rule.Conditions, workItemView))
                continue;

            var ruleType = rule.RuleType?.Trim().ToLowerInvariant();

            if (ruleType == RuleTypes.Validation)
            {
                var failed = rule.Validation is { ValueKind: JsonValueKind.Object }
                    ? !RuleConditionEvaluator.Evaluate(rule.Validation, workItemView)
                    : true;

                if (failed)
                {
                    errors.Add(new RuleValidationError
                    {
                        RuleId = rule.DataId ?? string.Empty,
                        RuleName = rule.Name,
                        Message = rule.ErrorMessage ?? $"Validation rule '{rule.Name}' failed.",
                        MessageTr = rule.ErrorMessage
                    });
                }
            }
            else
            {
                ApplyActions(rule, workItemView, mutations, sideEffects);
            }

            if (rule.StopProcessing == true)
                break;
        }

        if (matched.Count > 0)
        {
            _logger.LogDebug(
                "Rule phase {Phase} trigger {Trigger}: matched {Count}, errors {Errors}, mutations {Mutations}",
                phase,
                context.Trigger,
                matched.Count,
                errors.Count,
                mutations.Count);
        }

        return new RuleExecutionResult
        {
            FieldMutations = mutations,
            ValidationErrors = errors,
            SideEffects = sideEffects
        };
    }

    private static void ApplyActions(
        RuleRecord rule,
        IReadOnlyDictionary<string, object?> workItemView,
        Dictionary<string, object?> mutations,
        List<RuleSideEffect> sideEffects)
    {
        foreach (var action in RuleActionParser.ParseActions(rule.Actions))
        {
            var type = RuleActionParser.GetActionType(action)?.ToLowerInvariant();
            switch (type)
            {
                case "setfield":
                {
                    var field = RuleActionParser.GetString(action, "field");
                    if (string.IsNullOrWhiteSpace(field) || ForbiddenMutations.Contains(field))
                        break;

                    mutations[field] = RuleActionParser.GetValue(action, "value");
                    break;
                }
                case "setassignee":
                    mutations["assignee"] = RuleActionParser.GetValue(action, "value")
                        ?? RuleActionParser.GetString(action, "assignee");
                    break;
                case "setassignmentgroups":
                    mutations["assignmentGroups"] = RuleActionParser.GetValue(action, "value")
                        ?? RuleActionParser.GetValue(action, "groups");
                    break;
                case "addwatcher":
                {
                    sideEffects.Add(new RuleSideEffect
                    {
                        Type = "addWatcher",
                        Payload = new Dictionary<string, object?>
                        {
                            ["watcher"] = RuleActionParser.GetValue(action, "value")
                                ?? RuleActionParser.GetString(action, "watcher")
                        }
                    });
                    break;
                }
                case "createnotification":
                {
                    sideEffects.Add(new RuleSideEffect
                    {
                        Type = "createNotification",
                        Payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["templateKey"] = RuleActionParser.GetString(action, "templateKey"),
                            ["recipients"] = RuleActionParser.GetValue(action, "recipients")
                        }
                    });
                    break;
                }
                case "sendemailviamngnotifiers":
                {
                    sideEffects.Add(new RuleSideEffect
                    {
                        Type = "sendEmailViaMngNotifiers",
                        Payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["templateKey"] = RuleActionParser.GetString(action, "templateKey"),
                            ["recipients"] = RuleActionParser.GetValue(action, "recipients")
                        }
                    });
                    break;
                }
                case "createactivity":
                {
                    sideEffects.Add(new RuleSideEffect
                    {
                        Type = "createActivity",
                        Payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["summary"] = RuleActionParser.GetString(action, "summary")
                                ?? RuleActionParser.GetString(action, "message"),
                            ["activityType"] = RuleActionParser.GetString(action, "activityType") ?? "RuleAction"
                        }
                    });
                    break;
                }
                case "startworkflow":
                {
                    sideEffects.Add(new RuleSideEffect
                    {
                        Type = "startWorkflow",
                        Payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["workflowId"] = RuleActionParser.GetString(action, "workflowId"),
                            ["workflowVersionId"] = RuleActionParser.GetString(action, "workflowVersionId"),
                            ["triggerType"] = RuleActionParser.GetString(action, "triggerType") ?? "op_rules",
                            ["triggerData"] = RuleActionParser.GetValue(action, "triggerData")
                        }
                    });
                    break;
                }
                case "createdatasetrows":
                {
                    sideEffects.Add(new RuleSideEffect
                    {
                        Type = "createDatasetRows",
                        Payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["actionJson"] = action.GetRawText()
                        }
                    });
                    break;
                }
                case "updatedatasetrows":
                {
                    sideEffects.Add(new RuleSideEffect
                    {
                        Type = "updateDatasetRows",
                        Payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["actionJson"] = action.GetRawText()
                        }
                    });
                    break;
                }
                default:
                    break;
            }
        }
    }

    private static Dictionary<string, object?> MergeWorkItem(
        IReadOnlyDictionary<string, object?> source,
        IReadOnlyDictionary<string, object?> mutations)
    {
        var merged = new Dictionary<string, object?>(source, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in mutations)
            merged[key] = value;

        return merged;
    }
}
