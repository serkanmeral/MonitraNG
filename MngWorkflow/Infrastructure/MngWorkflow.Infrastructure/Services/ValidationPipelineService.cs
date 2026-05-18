using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Infrastructure.Services;

/// <summary>
/// Validation pipeline executor.
/// @wf_validation_pipelines'dan pipeline'ları okur ve çalıştırır.
/// </summary>
public class ValidationPipelineService : IValidationPipelineService
{
    private readonly IDataGatewayClient _dgClient;
    private readonly ILogger<ValidationPipelineService> _logger;

    public ValidationPipelineService(
        IDataGatewayClient dgClient,
        ILogger<ValidationPipelineService> logger)
    {
        _dgClient = dgClient;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(
        string datasetName,
        Dictionary<string, object> payload,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pipelines = await _dgClient.GetDataAsync(
                "@wf_validation_pipelines",
                $"dataset:eq:{datasetName}",
                domainName,
                authorizationHeader,
                cancellationToken);

            if (pipelines.Count == 0)
            {
                _logger.LogDebug("No validation pipelines for dataset {Dataset}", datasetName);
                return new ValidationResult(true);
            }

            foreach (var pipeline in pipelines.OrderBy(p => GetOrder(p)))
            {
                var result = await ExecutePipelineAsync(pipeline, payload, domainName, authorizationHeader, cancellationToken);
                if (!result.IsValid)
                    return result;
            }

            return new ValidationResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for dataset {Dataset}", datasetName);
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    private static int GetOrder(Dictionary<string, object> pipeline)
    {
        if (pipeline.TryGetValue("order", out var o) && o != null)
        {
            if (o is JsonElement je && je.TryGetInt32(out var i)) return i;
            if (o is int i2) return i2;
        }
        return 0;
    }

    private async Task<ValidationResult> ExecutePipelineAsync(
        Dictionary<string, object> pipeline,
        Dictionary<string, object> payload,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        if (!pipeline.TryGetValue("steps", out var stepsObj) || stepsObj == null)
            return new ValidationResult(true);

        var steps = stepsObj as List<object>;
        if (steps == null && stepsObj is JsonElement je)
        {
            steps = JsonSerializer.Deserialize<List<object>>(je.GetRawText()) ?? new List<object>();
        }
        if (steps == null || steps.Count == 0)
            return new ValidationResult(true);

        object? fetchResult = null;

        foreach (var stepObj in steps)
        {
            var step = stepObj as Dictionary<string, object>;
            if (step == null && stepObj is JsonElement stepJe)
            {
                step = JsonSerializer.Deserialize<Dictionary<string, object>>(stepJe.GetRawText());
            }
            if (step == null) continue;

            var type = GetString(step, "type");
            switch (type)
            {
                case "fetch":
                    fetchResult = await ExecuteFetchAsync(step, payload, domainName, authorizationHeader, cancellationToken);
                    break;
                case "assert":
                    var assertOk = EvaluateAssert(step, payload, fetchResult);
                    if (!assertOk)
                    {
                        var msg = GetString(step, "message") ?? "Assertion failed";
                        return new ValidationResult(false, msg);
                    }
                    break;
                case "return":
                    var isValid = GetBool(step, "isValid", true);
                    var errMsg = GetString(step, "errorMessage");
                    return new ValidationResult(isValid, errMsg);
                default:
                    _logger.LogWarning("Unknown pipeline step type: {Type}", type);
                    break;
            }
        }

        return new ValidationResult(true);
    }

    private async Task<object?> ExecuteFetchAsync(
        Dictionary<string, object> step,
        Dictionary<string, object> payload,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var dataset = GetString(step, "dataset");
        var byField = GetString(step, "by");
        var valuePath = GetString(step, "value");

        if (string.IsNullOrEmpty(dataset) || string.IsNullOrEmpty(byField))
        {
            _logger.LogWarning("Fetch step missing dataset or by field");
            return null;
        }

        object? value = null;
        if (!string.IsNullOrEmpty(valuePath))
            value = GetValueByPath(payload, valuePath);
        if (value == null && payload.TryGetValue(byField, out var v))
            value = v;

        if (value == null)
        {
            _logger.LogWarning("Fetch step: could not resolve value for {By}", byField);
            return null;
        }

        var valueStr = NormalizeScalar(value);
        if (string.IsNullOrEmpty(valueStr))
        {
            _logger.LogWarning("Fetch step: empty value for {By}", byField);
            return null;
        }

        var filter = $"{byField}:eq:{valueStr}";
        var data = await _dgClient.GetDataAsync(dataset, filter, domainName, authorizationHeader, cancellationToken);
        return data.FirstOrDefault();
    }

    private static bool EvaluateAssert(Dictionary<string, object> step, Dictionary<string, object> payload, object? fetchResult)
    {
        var expr = GetString(step, "expr");
        if (string.IsNullOrEmpty(expr)) return true;

        // Basit assert: result.key == payload.projectKey gibi
        // "result" -> fetchResult'tan, "payload" -> payload'dan
        // Şimdilik basit eşitlik kontrolü
        if (expr.Contains("result.", StringComparison.Ordinal) && fetchResult is Dictionary<string, object> resultDict)
        {
            var parts = expr.Split("==", StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var left = parts[0].Trim().Replace("result.", "", StringComparison.Ordinal);
                var right = parts[1].Trim().Replace("payload.", "", StringComparison.Ordinal);
                var leftVal = resultDict.TryGetValue(left, out var lv) ? NormalizeScalar(lv) : null;
                var rightVal = payload.TryGetValue(right, out var rv) ? NormalizeScalar(rv) : null;
                return string.Equals(leftVal, rightVal, StringComparison.Ordinal);
            }
        }
        return true;
    }

    /// <summary>Payload / DG satırlarında JsonElement ve diğer tipleri string karşılaştırmaya uyumlar.</summary>
    private static string? NormalizeScalar(object? v)
    {
        if (v == null) return null;
        if (v is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => je.ToString(),
            };
        }
        return v.ToString();
    }

    private static string? GetString(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v == null) return null;
        if (v is string s) return s;
        if (v is JsonElement je) return je.GetString();
        return v.ToString();
    }

    private static bool GetBool(Dictionary<string, object> dict, string key, bool defaultValue)
    {
        if (!dict.TryGetValue(key, out var v) || v == null) return defaultValue;
        if (v is bool b) return b;
        if (v is JsonElement je) return je.GetBoolean();
        return bool.TryParse(v.ToString(), out var result) && result;
    }

    private static object? GetValueByPath(Dictionary<string, object> data, string path)
    {
        var parts = path.Split('.');
        object? current = data;
        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> d && d.TryGetValue(part, out var next))
                current = next;
            else
                return null;
        }
        return current;
    }
}
