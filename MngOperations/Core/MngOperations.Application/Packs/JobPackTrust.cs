using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;

namespace MngOperations.Application.Packs;

public static class JobPackTrust
{
    public const string FirstParty = "firstParty";
    public const string ThirdParty = "thirdParty";
    public const string DefaultPublisher = "MonitraNG";

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static string NormalizeOrigin(string? origin, string defaultValue)
    {
        var value = (origin ?? string.Empty).Trim();
        if (string.Equals(value, FirstParty, StringComparison.OrdinalIgnoreCase)) return FirstParty;
        if (string.Equals(value, ThirdParty, StringComparison.OrdinalIgnoreCase)) return ThirdParty;
        return defaultValue;
    }

    public static void StampCatalog(JobPackDefinition pack)
    {
        var declared = NormalizeOrigin(pack.Origin, FirstParty);
        pack.Origin = declared;
        pack.Publisher = string.IsNullOrWhiteSpace(pack.Publisher)
            ? DefaultPublisher
            : pack.Publisher.Trim();
        pack.ContentSha256 = ComputeContentSha256(pack);
        pack.Verified = declared == FirstParty;
        pack.CanApply = pack.Verified;
    }

    public static void AssertCanApply(JobPackDefinition pack)
    {
        if (pack.CanApply && pack.Verified) return;
        throw new OperationCoreException(
            "PACK_UNTRUSTED",
            "Unsigned or third-party packs cannot be applied.",
            "İmzasız veya üçüncü taraf paket kurulamaz.",
            403);
    }

    public static string ComputeContentSha256(JobPackDefinition pack)
    {
        var canonical = new Dictionary<string, object?>
        {
            ["code"] = pack.Code?.Trim() ?? "",
            ["version"] = JobPackCatalog.NormalizeVersion(pack.Version),
            ["name"] = pack.Name?.Trim() ?? "",
            ["description"] = string.IsNullOrWhiteSpace(pack.Description) ? null : pack.Description.Trim(),
            ["kinds"] = pack.Kinds ?? [],
            ["folders"] = (pack.Folders ?? [])
                .Select(f => new Dictionary<string, object?> { ["name"] = f.Name ?? "" })
                .ToList(),
            ["wbs"] = CanonicalWbs(pack.Wbs),
            ["starters"] = (pack.Starters ?? []).Select(s => new Dictionary<string, object?>
            {
                ["folder"] = s.Folder ?? "",
                ["title"] = s.Title ?? "",
                ["kind"] = string.IsNullOrWhiteSpace(s.Kind) ? null : s.Kind,
                ["body"] = string.IsNullOrWhiteSpace(s.Body) ? null : s.Body
            }).ToList(),
            ["diagram"] = pack.Diagram is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["folder"] = pack.Diagram.Folder ?? "",
                    ["title"] = pack.Diagram.Title ?? "",
                    ["kind"] = string.IsNullOrWhiteSpace(pack.Diagram.Kind) ? null : pack.Diagram.Kind
                },
            ["workspace"] = CanonicalWorkspace(pack.Workspace)
        };

        var json = JsonSerializer.Serialize(canonical, CanonicalOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    public static JobPackInspectDto Inspect(JobPackDefinition? pack)
    {
        if (pack is null || string.IsNullOrWhiteSpace(pack.Code))
        {
            return new JobPackInspectDto
            {
                Origin = ThirdParty,
                Publisher = "",
                ContentSha256 = "",
                FromCatalog = false,
                Verified = false,
                CanApply = false,
                Reason = "PACK_EMPTY",
                ReasonTr = "Paket gövdesi boş."
            };
        }

        var hash = ComputeContentSha256(pack);
        var catalog = JobPackCatalog.Find(pack.Code);
        if (catalog is not null
            && string.Equals(catalog.ContentSha256, hash, StringComparison.OrdinalIgnoreCase))
        {
            return new JobPackInspectDto
            {
                Code = catalog.Code,
                Origin = catalog.Origin ?? FirstParty,
                Publisher = catalog.Publisher ?? DefaultPublisher,
                ContentSha256 = catalog.ContentSha256,
                FromCatalog = true,
                Verified = catalog.Verified,
                CanApply = catalog.CanApply
            };
        }

        var origin = NormalizeOrigin(pack.Origin, ThirdParty);
        return new JobPackInspectDto
        {
            Code = pack.Code.Trim(),
            Origin = origin,
            Publisher = string.IsNullOrWhiteSpace(pack.Publisher) ? "" : pack.Publisher.Trim(),
            ContentSha256 = hash,
            FromCatalog = false,
            Verified = false,
            CanApply = false,
            Reason = "PACK_UNTRUSTED",
            ReasonTr = "Paket imzalı katalogda değil; üçüncü taraf veya içeriği değişmiş."
        };
    }

    private static List<Dictionary<string, object?>> CanonicalWbs(List<JobPackWbsNode>? nodes)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var node in nodes ?? [])
        {
            var row = new Dictionary<string, object?>
            {
                ["name"] = node.Name ?? "",
                ["kind"] = string.IsNullOrWhiteSpace(node.Kind) ? "task" : node.Kind,
                ["weight"] = node.Weight
            };
            var children = CanonicalWbs(node.Children);
            if (children.Count > 0)
                row["children"] = children;
            list.Add(row);
        }

        return list;
    }

    private static Dictionary<string, object?>? CanonicalWorkspace(JobPackWorkspace? workspace)
    {
        if (workspace is null)
            return null;
        var rules = workspace.Rules ?? [];
        var sla = workspace.SlaPolicies ?? [];
        var dashboards = workspace.Dashboards ?? [];
        if (rules.Count == 0 && sla.Count == 0 && dashboards.Count == 0)
            return null;

        return new Dictionary<string, object?>
        {
            ["rules"] = rules.Select(r => new Dictionary<string, object?>
            {
                ["name"] = r.Name?.Trim() ?? "",
                ["description"] = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
                ["ruleType"] = r.RuleType ?? "",
                ["trigger"] = r.Trigger ?? "",
                ["transitionKey"] = string.IsNullOrWhiteSpace(r.TransitionKey) ? null : r.TransitionKey,
                ["applyMode"] = string.IsNullOrWhiteSpace(r.ApplyMode) ? null : r.ApplyMode,
                ["errorMessage"] = string.IsNullOrWhiteSpace(r.ErrorMessage) ? null : r.ErrorMessage,
                ["priority"] = r.Priority,
                ["conditions"] = CanonicalJson(r.Conditions)
            }).ToList(),
            ["slaPolicies"] = sla.Select(s => new Dictionary<string, object?>
            {
                ["name"] = s.Name?.Trim() ?? "",
                ["responseTargetMinutes"] = s.ResponseTargetMinutes,
                ["resolveTargetMinutes"] = s.ResolveTargetMinutes,
                ["priority"] = s.Priority
            }).ToList(),
            ["dashboards"] = dashboards.Select(d => new Dictionary<string, object?>
            {
                ["name"] = d.Name?.Trim() ?? "",
                ["description"] = string.IsNullOrWhiteSpace(d.Description) ? null : d.Description.Trim(),
                ["scope"] = string.IsNullOrWhiteSpace(d.Scope) ? "workspace" : d.Scope,
                ["isDefault"] = d.IsDefault,
                ["layout"] = CanonicalJson(d.Layout),
                ["widgets"] = CanonicalJson(d.Widgets)
            }).ToList()
        };
    }

    private static object? CanonicalJson(JsonElement? element)
    {
        if (element is null)
            return null;
        var value = element.Value;
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;
        return JsonSerializer.Deserialize<object>(value.GetRawText());
    }
}
