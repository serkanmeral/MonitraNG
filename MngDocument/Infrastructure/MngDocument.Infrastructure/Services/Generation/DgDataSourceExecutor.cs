using MngDocument.Application.Contracts.DataSources;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Interfaces;
using MngDocument.Infrastructure.Services.Generation.DataSources;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DgDataSourceExecutor : IDataSourceExecutor
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;

    public DgDataSourceExecutor(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<DataSourceExecutionResult> ExecuteAsync(
        TemplateValueSourceModel source,
        ParameterResolutionContext context,
        CancellationToken ct = default)
    {
        var provider = source.Provider?.Trim() ?? "dg";
        if (!string.Equals(provider, "dg", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Data source provider '{provider}' is not supported yet.");
        }

        var mode = source.Mode?.Trim().ToLowerInvariant() ?? "manual";
        return mode switch
        {
            "getbyid" => await ExecuteGetByIdAsync(source, context, ct),
            "querypage" => await ExecuteQueryPageAsync(source, context, ct),
            "namedquery" => await ExecuteNamedQueryAsync(source, context, ct),
            "context" => ExecuteContext(source, context),
            _ => throw new NotSupportedException($"Data source mode '{mode}' is not supported by DgDataSourceExecutor.")
        };
    }

    private async Task<DataSourceExecutionResult> ExecuteGetByIdAsync(
        TemplateValueSourceModel source,
        ParameterResolutionContext context,
        CancellationToken ct)
    {
        var dataset = RequireDataset(source);
        var idTemplate = string.IsNullOrWhiteSpace(source.IdFrom)
            ? "{{runtime.contextId}}"
            : source.IdFrom;
        var id = DataSourceTokenResolver.ResolveString(idTemplate, context).Trim();
        if (string.IsNullOrWhiteSpace(id))
            return EmptyRows();

        var row = await _dg.GetByIdAsync<Dictionary<string, object?>>(dataset, id, Token, ct);
        if (row is null)
            return EmptyRows();

        return new DataSourceExecutionResult
        {
            Shape = DataSourceResultShape.Single,
            Single = row
        };
    }

    private async Task<DataSourceExecutionResult> ExecuteQueryPageAsync(
        TemplateValueSourceModel source,
        ParameterResolutionContext context,
        CancellationToken ct)
    {
        var dataset = RequireDataset(source);
        var match = DataSourceTokenResolver.ResolveMatch(source.Match, context);
        var page = await _dg.QueryPageAsync(dataset, match, source.Query, Token, ct);
        var rows = page.Items
            .Select(r => (IReadOnlyDictionary<string, object?>)r)
            .ToList();

        if (rows.Count == 0 && PackageShipmentLinesQueryFallback.IsDirectPackageQuery(dataset, match))
        {
            var fallback = await PackageShipmentLinesQueryFallback.TryLoadAsync(
                _dg,
                match,
                source.Query,
                Token,
                ct);
            rows = fallback
                .Select(r => (IReadOnlyDictionary<string, object?>)r)
                .ToList();
        }

        return new DataSourceExecutionResult
        {
            Shape = DataSourceResultShape.Rows,
            Rows = rows
        };
    }

    private async Task<DataSourceExecutionResult> ExecuteNamedQueryAsync(
        TemplateValueSourceModel source,
        ParameterResolutionContext context,
        CancellationToken ct)
    {
        var dataset = RequireDataset(source);
        var queryName = source.QueryName?.Trim()
            ?? throw new InvalidOperationException("namedQuery requires queryName.");

        var parameters = DataSourceTokenResolver.ResolveMatch(source.Parameters, context);
        var rows = await _dg.ExecuteNamedQueryAsync(dataset, queryName, parameters, Token, ct);

        return new DataSourceExecutionResult
        {
            Shape = DataSourceResultShape.Rows,
            Rows = rows
                .Select(r => (IReadOnlyDictionary<string, object?>)r)
                .ToList()
        };
    }

    private static DataSourceExecutionResult ExecuteContext(
        TemplateValueSourceModel source,
        ParameterResolutionContext context)
    {
        var path = source.Path?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return new DataSourceExecutionResult
            {
                Shape = DataSourceResultShape.Single,
                Single = JsonObjectToDictionary(context.ContextTree)
            };
        }

        var value = DocumentContextPathResolver.GetStringWithFallback(
            context.ContextTree,
            path,
            source.FallbackPath);

        return new DataSourceExecutionResult
        {
            Shape = DataSourceResultShape.Scalar,
            Scalar = value
        };
    }

    private static string RequireDataset(TemplateValueSourceModel source)
    {
        var dataset = source.Dataset?.Trim();
        if (string.IsNullOrWhiteSpace(dataset))
            throw new InvalidOperationException("Data source requires dataset.");
        return dataset;
    }

    private static DataSourceExecutionResult EmptyRows() =>
        new() { Shape = DataSourceResultShape.Rows, Rows = Array.Empty<IReadOnlyDictionary<string, object?>>() };

    private static IReadOnlyDictionary<string, object?> JsonObjectToDictionary(System.Text.Json.Nodes.JsonObject tree)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in tree)
            dict[kv.Key] = kv.Value is null ? null : DocumentContextPathResolver.ToJsonObject(kv.Value);
        return dict;
    }
}
