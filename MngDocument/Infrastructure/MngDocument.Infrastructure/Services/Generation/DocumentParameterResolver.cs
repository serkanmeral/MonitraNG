using System.Globalization;
using System.Text.Json.Nodes;
using MngDocument.Application.Configuration;
using MngDocument.Infrastructure.Services;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DocumentParameterResolver
{
    private readonly DocumentIncrementalAllocator _incremental;

    public DocumentParameterResolver(DocumentIncrementalAllocator incremental)
    {
        _incremental = incremental;
    }

    public async Task<Dictionary<string, string>> ResolveAsync(
        TemplateModelDocument model,
        JsonObject contextTree,
        IReadOnlyDictionary<string, string>? profileDefaults,
        IReadOnlyDictionary<string, string>? overrides,
        string? token,
        CancellationToken ct)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in model.Parameters)
        {
            var key = param.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (TemplateModelSerializer.IsHeaderBoundLetterheadDocNo(param))
                continue;

            if (TemplateModelSerializer.IsHeaderBoundLetterheadCreatePerson(param))
                continue;

            if (overrides is not null && overrides.TryGetValue(key, out var overrideValue))
            {
                values[key] = overrideValue ?? string.Empty;
                continue;
            }

            var mode = param.ValueSourceMode?.Trim() ?? "manual";
            values[key] = mode.ToLowerInvariant() switch
            {
                "incremental" when param.Incremental is not null =>
                    await _incremental.AllocateAsync(param.Incremental, token, ct),
                "context" => ResolveContext(param, contextTree),
                "generated" => FormatGenerated(param),
                "static" => param.DefaultValue ?? param.ContextBinding?.DefaultValue ?? string.Empty,
                _ => ResolveManual(param, profileDefaults)
            };
        }

        if (profileDefaults is not null)
        {
            foreach (var kv in profileDefaults)
            {
                if (!values.ContainsKey(kv.Key) || string.IsNullOrWhiteSpace(values[kv.Key]))
                    values[kv.Key] = kv.Value;
            }
        }

        return values;
    }

    private static string ResolveManual(
        TemplateParameterModel param,
        IReadOnlyDictionary<string, string>? profileDefaults)
    {
        if (!string.IsNullOrWhiteSpace(param.DefaultValue))
            return param.DefaultValue.Trim();

        if (profileDefaults is not null
            && profileDefaults.TryGetValue(param.Key, out var fromProfile)
            && !string.IsNullOrWhiteSpace(fromProfile))
            return fromProfile;

        return param.Label ?? param.Key;
    }

    private static string ResolveContext(TemplateParameterModel param, JsonObject contextTree)
    {
        var binding = param.ContextBinding;
        if (binding is null)
            return param.DefaultValue ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(binding.Format))
        {
            var formatted = DocumentContextPathResolver.ApplyFormat(contextTree, binding.Format);
            if (!string.IsNullOrWhiteSpace(formatted))
                return binding.Format.Contains("{quantity}", StringComparison.Ordinal)
                    ? formatted.ToUpperInvariant()
                    : formatted;
        }

        var value = DocumentContextPathResolver.GetStringWithFallback(
            contextTree,
            binding.Path,
            binding.FallbackPath);

        if (!string.IsNullOrWhiteSpace(value))
        {
            if (string.Equals(param.DataType, "bool", StringComparison.OrdinalIgnoreCase))
                return FormatBool(value);

            if (string.Equals(param.DataType, "date", StringComparison.OrdinalIgnoreCase)
                || string.Equals(param.DataType, "datetime", StringComparison.OrdinalIgnoreCase))
                return FormatDate(value, param.Format ?? binding.Format);

            return value;
        }

        return binding.DefaultValue ?? param.DefaultValue ?? string.Empty;
    }

    private static string FormatGenerated(TemplateParameterModel param)
    {
        var now = DateTime.UtcNow;
        var fmt = param.Format ?? param.ContextBinding?.Format ?? "dd.MM.yyyy";
        return now.ToString(fmt, CultureInfo.InvariantCulture);
    }

    private static string FormatBool(string raw)
    {
        if (bool.TryParse(raw, out var value))
            return value ? "Evet" : "Hayır";

        return raw;
    }

    private static string FormatDate(string raw, string? format)
    {
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out dt))
            return dt.ToString(format ?? "dd.MM.yyyy", CultureInfo.InvariantCulture);

        return raw;
    }
}
