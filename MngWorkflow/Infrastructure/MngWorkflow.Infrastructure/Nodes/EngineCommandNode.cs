using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Infrastructure.Templates;

namespace MngWorkflow.Infrastructure.Nodes;

/// <summary>
/// Publishes a command to monitoring engine MQTT topic via MngReactor /api/v1/mqtt/publish.
/// Topic: monitoring/{domain}/engine/{engineId}/command
/// </summary>
public sealed class EngineCommandNode(IServiceScopeFactory scopeFactory) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.EngineCommand;

    public Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken) =>
        ExecuteCoreAsync(scopeFactory, context, node, defaultCommand: null, cancellationToken);

    internal static async Task<NodeExecutionResult> ExecuteCoreAsync(
        IServiceScopeFactory scopeFactory,
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        string? defaultCommand,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var templates = scope.ServiceProvider.GetRequiredService<IWorkflowContextTemplateResolver>();
        var keeper = scope.ServiceProvider.GetRequiredService<IWorkflowKeeperAuthClient>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<MngWorkflowSettings>>().Value.EngineCommand;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EngineCommandNode>>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        var command = ResolveCommand(node, defaultCommand);
        if (string.IsNullOrWhiteSpace(command))
            return NodeExecutionResult.Fail("engine.command: command is required", retryable: false);

        var engineId = templates.ResolveOptional(context, context.DomainName, GetString(node, "engineId"))
            ?? settings.DefaultEngineId;
        if (string.IsNullOrWhiteSpace(engineId))
            return NodeExecutionResult.Fail("engine.command: engineId is required", retryable: false);

        var ipAddress = templates.ResolveOptional(context, context.DomainName, GetString(node, "ipAddress"));
        if (string.IsNullOrWhiteSpace(ipAddress)
            && (command is "block_ip" or "unblock_ip"))
        {
            return NodeExecutionResult.Fail("engine.command: ipAddress is required for block/unblock", retryable: false);
        }

        var ttlMinutes = ParseInt(node, "ttlMinutes");
        var reason = templates.ResolveOptional(context, context.DomainName, GetString(node, "reason"));
        var topic = BuildCommandTopic(settings, context.DomainName, engineId.Trim());

        var commandBody = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["correlationId"] = context.CorrelationId,
            ["instanceId"] = context.InstanceId,
            ["nodeId"] = node.Id,
            ["domainName"] = context.DomainName,
            ["domainId"] = context.DomainId,
            ["publishedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(ipAddress))
            commandBody["ipAddress"] = ipAddress;
        if (ttlMinutes.HasValue)
            commandBody["ttlMinutes"] = ttlMinutes.Value;
        if (!string.IsNullOrWhiteSpace(reason))
            commandBody["reason"] = reason;

        var messageJson = JsonSerializer.Serialize(commandBody);

        if (settings.DevLogOnly)
        {
            logger.LogInformation(
                "Engine command (dev-log-only) topic={Topic} command={Command} ip={Ip} instance={InstanceId}",
                topic,
                command,
                ipAddress,
                context.InstanceId);

            return NodeExecutionResult.Ok(output: new Dictionary<string, object?>
            {
                ["mode"] = "dev_log_only",
                ["topic"] = topic,
                ["command"] = command,
                ["ipAddress"] = ipAddress,
                ["engineId"] = engineId,
                ["messagePreview"] = Trim(messageJson, 500)
            });
        }

        var token = await keeper.GetServiceAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return NodeExecutionResult.Fail("engine.command: service account token unavailable", retryable: true);

        var baseUrl = settings.ReactorBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/mqtt/publish";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(settings.HttpTimeoutSeconds));

        var client = httpFactory.CreateClient("MngReactor");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { topic, message = messageJson });

        try
        {
            var response = await client.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var retryable = (int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout;
                return NodeExecutionResult.Fail(
                    $"engine.command HTTP {(int)response.StatusCode}: {Trim(body, 300)}",
                    retryable: retryable);
            }

            logger.LogInformation(
                "Engine command published topic={Topic} command={Command} instance={InstanceId}",
                topic,
                command,
                context.InstanceId);

            return NodeExecutionResult.Ok(output: new Dictionary<string, object?>
            {
                ["mode"] = "reactor_mqtt",
                ["topic"] = topic,
                ["command"] = command,
                ["ipAddress"] = ipAddress,
                ["engineId"] = engineId,
                ["statusCode"] = (int)response.StatusCode
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NodeExecutionResult.Fail("engine.command: Reactor publish timeout", retryable: true);
        }
        catch (HttpRequestException ex)
        {
            return NodeExecutionResult.Fail($"engine.command: {ex.Message}", retryable: true);
        }
    }

    private static string? ResolveCommand(WorkflowNodeDefinition node, string? defaultCommand)
    {
        var raw = GetString(node, "command") ?? defaultCommand;
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    public static string BuildCommandTopic(EngineCommandSettings settings, string domainName, string engineId)
    {
        var topic = $"monitoring/{domainName}/engine/{engineId}/command";
        if (string.IsNullOrWhiteSpace(settings.MqttTopicPrefix))
            return topic;

        return $"{settings.MqttTopicPrefix.Trim().TrimEnd('/')}/{topic}";
    }

    private static string? GetString(WorkflowNodeDefinition node, string key) =>
        node.Config.TryGetValue(key, out var raw) ? raw?.ToString() : null;

    private static int? ParseInt(WorkflowNodeDefinition node, string key)
    {
        if (!node.Config.TryGetValue(key, out var raw) || raw == null)
            return null;

        if (raw is int i)
            return i;
        if (raw is long l)
            return (int)l;
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var parsed))
            return parsed;

        return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

/// <summary>block.ip — engine.command preset (command=block_ip).</summary>
public sealed class BlockIpNode(IServiceScopeFactory scopeFactory) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.BlockIp;

    public Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken) =>
        EngineCommandNode.ExecuteCoreAsync(scopeFactory, context, node, defaultCommand: "block_ip", cancellationToken);
}
