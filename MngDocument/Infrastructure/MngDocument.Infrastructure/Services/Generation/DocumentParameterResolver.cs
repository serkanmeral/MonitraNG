using System.Globalization;
using System.Text.Json.Nodes;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Interfaces;
using MngDocument.Infrastructure.Services;
using MngDocument.Infrastructure.Services.Generation.DataSources;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DocumentParameterResolver
{
    private readonly DocumentIncrementalAllocator _incremental;
    private readonly IDataSourceExecutor _dataSources;
    private readonly DocumentDataSourceCatalogProvider _dataSourceCatalog;
    private readonly IDomainLogoProvider _logoProvider;

    public DocumentParameterResolver(
        DocumentIncrementalAllocator incremental,
        IDataSourceExecutor dataSources,
        DocumentDataSourceCatalogProvider dataSourceCatalog,
        IDomainLogoProvider logoProvider)
    {
        _incremental = incremental;
        _dataSources = dataSources;
        _dataSourceCatalog = dataSourceCatalog;
        _logoProvider = logoProvider;
    }

    public async Task<ParameterResolutionResult> ResolveAsync(
        TemplateModelDocument model,
        ParameterResolutionContext resolutionContext,
        IReadOnlyDictionary<string, string>? profileDefaults,
        IReadOnlyDictionary<string, string>? overrides,
        string? token,
        CancellationToken ct,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? tableOverrides = null)
    {
        var result = new ParameterResolutionResult();

        foreach (var param in model.Parameters)
        {
            var key = param.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (TemplateModelSerializer.IsHeaderBoundLetterheadDocNo(param))
                continue;

            if (TemplateModelSerializer.IsHeaderBoundLetterheadCreatePerson(param))
                continue;

            var kind = param.Kind?.Trim().ToLowerInvariant() ?? "scalar";
            if (string.Equals(kind, "image", StringComparison.OrdinalIgnoreCase))
            {
                var image = await ResolveImageParameterAsync(param, token, ct);
                if (image is not null)
                    result.Images[key] = image;
                continue;
            }

            if (string.Equals(kind, "table", StringComparison.OrdinalIgnoreCase)
                && tableOverrides is not null
                && TryGetTableOverride(tableOverrides, key, out var tableRows))
            {
                result.Tables[key] = tableRows;
                continue;
            }

            if (overrides is not null && overrides.TryGetValue(key, out var overrideValue))
            {
                result.Scalars[key] = overrideValue ?? string.Empty;
                continue;
            }

            var valueSource = await ResolveValueSourceAsync(param, ct);
            if (valueSource is not null)
            {
                await ResolveFromValueSourceAsync(param, kind, valueSource, resolutionContext, result, token, ct);
                continue;
            }

            if (string.Equals(kind, "table", StringComparison.OrdinalIgnoreCase))
            {
                result.Tables[key] = Array.Empty<IReadOnlyDictionary<string, object?>>();
                continue;
            }

            result.Scalars[key] = await ResolveLegacyAsync(
                param,
                resolutionContext.ContextTree,
                profileDefaults,
                token,
                ct);
        }

        if (profileDefaults is not null)
        {
            foreach (var kv in profileDefaults)
            {
                if (!result.Scalars.ContainsKey(kv.Key) || string.IsNullOrWhiteSpace(result.Scalars[kv.Key]))
                    result.Scalars[kv.Key] = kv.Value;
            }
        }

        return result;
    }

    private static bool TryGetTableOverride(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> tableOverrides,
        string key,
        out IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (tableOverrides.TryGetValue(key, out var direct) && direct is not null)
        {
            rows = direct;
            return true;
        }

        foreach (var kv in tableOverrides)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && kv.Value is not null)
            {
                rows = kv.Value;
                return true;
            }
        }

        rows = Array.Empty<IReadOnlyDictionary<string, object?>>();
        return false;
    }

    private async Task<TemplateValueSourceModel?> ResolveValueSourceAsync(
        TemplateParameterModel param,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(param.DataSourceRef))
        {
            var fromCatalog = await _dataSourceCatalog.TryGetValueSourceAsync(param.DataSourceRef, ct);
            if (fromCatalog is not null)
                return fromCatalog;
        }

        return param.ValueSource;
    }

    private async Task ResolveFromValueSourceAsync(
        TemplateParameterModel param,
        string kind,
        TemplateValueSourceModel source,
        ParameterResolutionContext resolutionContext,
        ParameterResolutionResult result,
        string? token,
        CancellationToken ct)
    {
        var mode = source.Mode?.Trim().ToLowerInvariant() ?? "manual";

        if (string.Equals(mode, "context", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(source.Path))
        {
            if (string.Equals(kind, "table", StringComparison.OrdinalIgnoreCase))
            {
                result.Tables[param.Key] = Array.Empty<IReadOnlyDictionary<string, object?>>();
                return;
            }

            result.Scalars[param.Key] = ResolveContextPath(
                source.Path,
                source.FallbackPath,
                param,
                resolutionContext.ContextTree);
            return;
        }

        var execution = await _dataSources.ExecuteAsync(source, resolutionContext, ct);

        if (string.Equals(kind, "table", StringComparison.OrdinalIgnoreCase))
        {
            result.Tables[param.Key] = execution.Rows;
            return;
        }

        result.Scalars[param.Key] = ExtractScalarFromExecution(param, source, execution, resolutionContext.ContextTree);
    }

    private static string ExtractScalarFromExecution(
        TemplateParameterModel param,
        TemplateValueSourceModel source,
        Application.Contracts.DataSources.DataSourceExecutionResult execution,
        JsonObject contextTree)
    {
        var field = source.Field?.Trim();
        if (execution.Shape == Application.Contracts.DataSources.DataSourceResultShape.Scalar)
            return FormatValue(param, execution.Scalar?.ToString());

        if (execution.Shape == Application.Contracts.DataSources.DataSourceResultShape.Single
            && execution.Single is not null)
        {
            var path = field ?? source.Path;
            if (!string.IsNullOrWhiteSpace(path))
                return FormatValue(param, GetFieldFromRecord(execution.Single, path));

            return string.Empty;
        }

        if (execution.Rows.Count > 0)
        {
            var path = field ?? source.Path;
            if (!string.IsNullOrWhiteSpace(path))
                return FormatValue(param, GetFieldFromRecord(execution.Rows[0], path));
        }

        if (!string.IsNullOrWhiteSpace(source.Path))
            return ResolveContextPath(source.Path, source.FallbackPath, param, contextTree);

        return source.DefaultValue ?? param.DefaultValue ?? string.Empty;
    }

    private static string GetFieldFromRecord(IReadOnlyDictionary<string, object?> record, string path)
    {
        var tree = DocumentContextPathResolver.ToJsonObject(record);
        return DocumentContextPathResolver.GetStringWithFallback(tree, path, null) ?? string.Empty;
    }

    private async Task<string> ResolveLegacyAsync(
        TemplateParameterModel param,
        JsonObject contextTree,
        IReadOnlyDictionary<string, string>? profileDefaults,
        string? token,
        CancellationToken ct)
    {
        var mode = param.ValueSourceMode?.Trim() ?? "manual";
        return mode.ToLowerInvariant() switch
        {
            "incremental" when param.Incremental is not null =>
                await _incremental.AllocateAsync(param.Incremental, token, ct),
            "context" => ResolveContext(param, contextTree),
            "generated" => FormatGenerated(param),
            "static" => param.DefaultValue ?? param.ContextBinding?.DefaultValue ?? string.Empty,
            _ => ResolveManual(param, profileDefaults)
        };
    }

    private static string ResolveContextPath(
        string path,
        string? fallbackPath,
        TemplateParameterModel param,
        JsonObject contextTree)
    {
        if (!string.IsNullOrWhiteSpace(param.ContextBinding?.Format)
            && param.ContextBinding.Format.Contains('{', StringComparison.Ordinal))
        {
            var formatted = DocumentContextPathResolver.ApplyFormat(contextTree, param.ContextBinding.Format);
            if (!string.IsNullOrWhiteSpace(formatted))
                return param.ContextBinding.Format.Contains("{quantity}", StringComparison.Ordinal)
                    ? formatted.ToUpperInvariant()
                    : formatted;
        }

        var value = DocumentContextPathResolver.GetStringWithFallback(contextTree, path, fallbackPath);
        return FormatValue(param, value) is { Length: > 0 } formattedValue
            ? formattedValue
            : param.ContextBinding?.DefaultValue ?? param.DefaultValue ?? string.Empty;
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

        return ResolveContextPath(binding.Path, binding.FallbackPath, param, contextTree);
    }

    private static string FormatGenerated(TemplateParameterModel param)
    {
        var now = DateTime.UtcNow;
        var fmt = param.Format ?? param.ContextBinding?.Format ?? "dd.MM.yyyy";
        return now.ToString(fmt, CultureInfo.InvariantCulture);
    }

    private static string FormatValue(TemplateParameterModel param, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        if (string.Equals(param.DataType, "bool", StringComparison.OrdinalIgnoreCase))
            return FormatBool(raw);

        if (string.Equals(param.DataType, "date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(param.DataType, "datetime", StringComparison.OrdinalIgnoreCase))
            return FormatDate(raw, param.Format ?? param.ValueSource?.Format);

        return raw.Trim();
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

    private async Task<ResolvedImageParameter?> ResolveImageParameterAsync(
        TemplateParameterModel param,
        string? token,
        CancellationToken ct)
    {
        var mode = param.ValueSourceMode?.Trim().ToLowerInvariant() ?? "manual";
        return mode switch
        {
            "domain" => await ResolveDomainLogoAsync(token, ct),
            "static" => ResolveStaticLogo(param.DefaultValue),
            _ => null
        };
    }

    private async Task<ResolvedImageParameter?> ResolveDomainLogoAsync(string? token, CancellationToken ct)
    {
        var logo = await _logoProvider.GetCurrentDomainLogoAsync(token, ct);
        return logo is null
            ? null
            : new ResolvedImageParameter { Bytes = logo.Bytes, Extension = logo.Extension };
    }

    private static ResolvedImageParameter? ResolveStaticLogo(string? raw)
    {
        var logo = DomainLogoProvider.TryDecodePayload(raw);
        return logo is null
            ? null
            : new ResolvedImageParameter { Bytes = logo.Bytes, Extension = logo.Extension };
    }
}
