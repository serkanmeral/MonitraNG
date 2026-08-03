using System.Text.RegularExpressions;
using MngReactor.Application.Contracts.SecEvents;

namespace MngReactor.Application.Services.SecEvents;

public static class SecEventParseRuleValidator
{
    public static readonly HashSet<string> AllowedTargetFields =
        new(SecEventTargetFieldCatalog.AllowedNames, StringComparer.Ordinal);

    public static readonly HashSet<string> AllowedExtractTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "event_data",
        "json_path",
        "regex",
        "kv",
        "constant"
    };

    public static readonly HashSet<string> AllowedWhenOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq",
        "neq",
        "in",
        "exists",
        "contains"
    };

    public static readonly HashSet<string> AllowedMessageFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "sshd_failed_password",
        "sshd_accepted",
        "sudo_command",
        "sudo_not_allowed"
    };

    private static readonly Regex RuleIdPattern = new(
        @"^[a-z0-9]+(?:[._-][a-z0-9]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void ValidateUpsert(SecEventParseRuleUpsertRequest request, bool isCreate)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ruleId = NormalizeRuleId(request.RuleId);
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("RuleId is required.");
        if (ruleId.Length > 128)
            throw new ArgumentException("RuleId must be at most 128 characters.");
        if (!RuleIdPattern.IsMatch(ruleId))
            throw new ArgumentException("RuleId must be lowercase dotted/slug form (e.g. windows.logon.4625).");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");
        if (request.Name.Trim().Length > 200)
            throw new ArgumentException("Name must be at most 200 characters.");

        if (request.Priority is < 0 or > 10_000)
            throw new ArgumentException("Priority must be between 0 and 10000.");

        var onConflict = string.IsNullOrWhiteSpace(request.OnConflict)
            ? "first_wins"
            : request.OnConflict.Trim();
        if (!string.Equals(onConflict, "first_wins", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OnConflict v1 only supports 'first_wins'.");

        ValidateMatch(request.Match);
        NormalizeExtractTargets(request.Extract);
        ValidateExtract(request.Extract);

        // Silence unused for create vs update — same rules today.
        _ = isCreate;
    }

    private static void NormalizeExtractTargets(List<SecEventParseRuleExtractStepDto>? extract)
    {
        if (extract is null)
            return;
        foreach (var step in extract)
        {
            step.To = NormalizeTargetField(step.To);
            if (step.Groups is null || step.Groups.Count == 0)
                continue;
            var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in step.Groups)
                normalized[key] = NormalizeTargetField(value) ?? value;
            step.Groups = normalized;
        }
    }

    private static string? NormalizeTargetField(string? to)
    {
        var field = (to ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(field))
            return field;
        if (SecEventTargetFieldCatalog.AllowedNames.Contains(field))
            return field;
        // Bare slug or custom.* → normalize into custom.<slug>
        if (field.StartsWith("custom.", StringComparison.OrdinalIgnoreCase) || !field.Contains('.'))
            return SecEventTargetFieldCatalog.NormalizeCustomFieldName(field);
        return field;
    }

    public static string NormalizeRuleId(string? ruleId) =>
        (ruleId ?? string.Empty).Trim().ToLowerInvariant();

    private static void ValidateMatch(SecEventParseRuleMatchDto? match)
    {
        if (match is null)
            throw new ArgumentException("Match is required.");

        if (match.SourceProduct is null || match.SourceProduct.Count == 0)
            throw new ArgumentException("Match.sourceProduct is required.");

        foreach (var p in match.SourceProduct)
        {
            if (string.IsNullOrWhiteSpace(p))
                throw new ArgumentException("Match.sourceProduct entries must be non-empty.");
            if (p.Trim().Length > 64)
                throw new ArgumentException("Match.sourceProduct entry is too long.");
        }

        if (match.When is not null)
        {
            foreach (var when in match.When)
            {
                if (string.IsNullOrWhiteSpace(when.Field))
                    throw new ArgumentException("Match.when.field is required.");
                var op = (when.Op ?? string.Empty).Trim().ToLowerInvariant();
                if (!AllowedWhenOps.Contains(op))
                    throw new ArgumentException($"Match.when.op '{when.Op}' is not allowed.");
                if (op is "eq" or "neq" && string.IsNullOrWhiteSpace(when.Value))
                    throw new ArgumentException($"Match.when value is required for op '{op}'.");
                if (op == "in" && (when.Values is null || when.Values.Count == 0))
                    throw new ArgumentException("Match.when.values is required for op 'in'.");
            }
        }

        if (match.MessagePatterns is not null)
        {
            foreach (var mp in match.MessagePatterns)
            {
                var family = (mp.Family ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(family))
                    throw new ArgumentException("Match.messagePatterns.family is required.");
                if (!AllowedMessageFamilies.Contains(family))
                    throw new ArgumentException($"Message pattern family '{family}' is not in the whitelist.");
            }
        }
    }

    private static void ValidateExtract(List<SecEventParseRuleExtractStepDto>? extract)
    {
        if (extract is null || extract.Count == 0)
            throw new ArgumentException("Extract must contain at least one step.");

        foreach (var step in extract)
        {
            var type = (step.Type ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedExtractTypes.Contains(type))
                throw new ArgumentException($"Extract type '{step.Type}' is not allowed.");

            switch (type)
            {
                case "constant":
                    RequireTarget(step.To);
                    if (step.Value is null)
                        throw new ArgumentException("Extract constant requires value.");
                    break;
                case "event_data":
                case "json_path":
                    if (string.IsNullOrWhiteSpace(step.From))
                        throw new ArgumentException($"Extract {type} requires from.");
                    RequireTarget(step.To);
                    break;
                case "regex":
                    if (string.IsNullOrWhiteSpace(step.Pattern))
                        throw new ArgumentException("Extract regex requires pattern.");
                    try
                    {
                        _ = new Regex(step.Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException($"Extract regex pattern is invalid: {ex.Message}");
                    }

                    if (step.Groups is null || step.Groups.Count == 0)
                        throw new ArgumentException("Extract regex requires groups map.");
                    foreach (var target in step.Groups.Values)
                        RequireTarget(target);
                    break;
                case "kv":
                    // kv may map multiple keys via groups, or single to
                    if (step.Groups is { Count: > 0 })
                    {
                        foreach (var target in step.Groups.Values)
                            RequireTarget(target);
                    }
                    else
                    {
                        RequireTarget(step.To);
                        if (string.IsNullOrWhiteSpace(step.From))
                            throw new ArgumentException("Extract kv requires from key when groups are omitted.");
                    }

                    break;
            }
        }
    }

    private static void RequireTarget(string? to)
    {
        var field = (to ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("Extract target field (to) is required.");
        if (!SecEventTargetFieldCatalog.IsAllowed(field))
            throw new ArgumentException(
                $"Extract target '{field}' is not allowed. Use a core field or custom.<slug>.");
    }

    /// <summary>Collects distinct custom.* targets from extract steps (already validated).</summary>
    public static IReadOnlyList<string> CollectCustomTargets(List<SecEventParseRuleExtractStepDto>? extract)
    {
        if (extract is null || extract.Count == 0)
            return [];

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in extract)
        {
            TryAdd(set, step.To);
            if (step.Groups is null)
                continue;
            foreach (var target in step.Groups.Values)
                TryAdd(set, target);
        }

        return set.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static void TryAdd(HashSet<string> set, string? to)
    {
        var field = (to ?? string.Empty).Trim().ToLowerInvariant();
        if (SecEventTargetFieldCatalog.IsCustomField(field))
            set.Add(field);
    }
}
