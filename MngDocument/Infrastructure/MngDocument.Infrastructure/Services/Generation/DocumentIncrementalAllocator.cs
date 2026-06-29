using System.Globalization;
using System.Text.Json.Nodes;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Services;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DocumentIncrementalAllocator
{
    private readonly IMngDataGatewayClient _dg;

    public DocumentIncrementalAllocator(IMngDataGatewayClient dg)
    {
        _dg = dg;
    }

    public async Task<string> AllocateAsync(
        TemplateIncrementalModel incremental,
        string? token,
        CancellationToken ct)
    {
        var format = incremental.Format?.Trim() ?? "{0}";
        var counterKey = BuildCounterKey(incremental);
        var nextValue = await GetNextValueAsync(counterKey, incremental.StartValue, incremental.IncrementStep, token, ct);
        return FormatIncrementalValue(format, nextValue);
    }

    internal static string BuildCounterKey(TemplateIncrementalModel incremental)
    {
        var scope = string.IsNullOrWhiteSpace(incremental.ScopeKey) ? "default" : incremental.ScopeKey.Trim();
        if (string.Equals(incremental.ResetPolicy, "yearly", StringComparison.OrdinalIgnoreCase))
            return $"{scope}-{DateTime.UtcNow:yy}";

        return scope;
    }

    internal static string FormatIncrementalValue(string format, long counterValue)
    {
        var now = DateTime.UtcNow;
        var result = format;
        result = result.Replace("{yyyy}", now.ToString("yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        result = result.Replace("{yy}", now.ToString("yy", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        result = result.Replace("{MM}", now.ToString("MM", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        result = result.Replace("{dd}", now.ToString("dd", CultureInfo.InvariantCulture), StringComparison.Ordinal);

        if (result.Contains("{0:", StringComparison.Ordinal))
        {
            var match = System.Text.RegularExpressions.Regex.Match(result, @"\{0:([^}]+)\}");
            if (match.Success)
            {
                var spec = match.Groups[1].Value;
                var formatted = counterValue.ToString(spec, CultureInfo.InvariantCulture);
                result = result.Replace(match.Value, formatted, StringComparison.Ordinal);
            }
        }

        result = result.Replace("{0}", counterValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return result;
    }

    private async Task<long> GetNextValueAsync(
        string counterKey,
        int startValue,
        int incrementStep,
        string? token,
        CancellationToken ct)
    {
        var step = Math.Max(1, incrementStep);
        var start = Math.Max(1, startValue);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var page = await _dg.QueryPageAsync(
                DmDatasets.GenerationCounters,
                new Dictionary<string, object?> { ["counterKey"] = counterKey },
                "limit=1",
                token,
                ct);

            if (page.Items.Count == 0)
            {
                try
                {
                    var created = await _dg.CreateAsync<Dictionary<string, object?>>(
                        DmDatasets.GenerationCounters,
                        new Dictionary<string, object?>
                        {
                            ["counterKey"] = counterKey,
                            ["value"] = start
                        },
                        token,
                        ct);

                    var createdValue = ReadLong(created, "value");
                    return createdValue > 0 ? createdValue : start;
                }
                catch
                {
                    continue;
                }
            }

            var row = page.Items[0];
            var id = ReadString(row, "__dataId") ?? ReadString(row, "dataId");
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Counter row id missing.");

            var current = ReadLong(row, "value");
            if (current < start)
                current = start - step;

            var next = current + step;
            await _dg.UpdateAsync<Dictionary<string, object?>>(
                DmDatasets.GenerationCounters,
                id,
                new Dictionary<string, object?> { ["value"] = next },
                token,
                ct);

            return next;
        }

        throw new InvalidOperationException($"Could not allocate counter for key '{counterKey}'.");
    }

    private static long ReadLong(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return 0;

        return raw switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            _ => long.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var n) ? n : 0
        };
    }

    private static string? ReadString(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var raw) && raw is not null
            ? Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim()
            : null;
}
