using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Application.Smoke;

/// <summary>Faz 1 kabul senaryosu: Manual → If → HTTP → Log</summary>
public static class SmokeWorkflowDefinition
{
    public const string WorkflowId = "smoke-manual-if-http-log";
    public const string VersionId = "smoke-manual-if-http-log-v1";

    public static WorkflowVersionDocument Create(string domainId, string domainName) =>
        new()
        {
            Id = VersionId,
            WorkflowId = WorkflowId,
            DomainId = domainId,
            DomainName = domainName,
            Version = 1,
            Status = WorkflowVersionStatus.Published,
            PublishedAt = DateTime.UtcNow,
            EntryNodeId = "manual_1",
            Nodes =
            [
                new WorkflowNodeDefinition { Id = "manual_1", Type = WorkflowNodeTypes.ManualTrigger },
                new WorkflowNodeDefinition
                {
                    Id = "if_1",
                    Type = WorkflowNodeTypes.If,
                    Config = new Dictionary<string, object?>
                    {
                        ["field"] = "event.value",
                        ["operator"] = "gt",
                        ["value"] = 5
                    }
                },
                new WorkflowNodeDefinition
                {
                    Id = "http_1",
                    Type = WorkflowNodeTypes.HttpRequest,
                    Config = new Dictionary<string, object?>
                    {
                        ["method"] = "GET",
                        ["url"] = "http://mnggateway:5000/health"
                    }
                },
                new WorkflowNodeDefinition
                {
                    Id = "log_1",
                    Type = WorkflowNodeTypes.WriteLog,
                    Config = new Dictionary<string, object?> { ["message"] = "Smoke workflow completed" }
                }
            ],
            Edges =
            [
                new WorkflowEdgeDefinition { FromNodeId = "manual_1", ToNodeId = "if_1", EdgeKey = "default" },
                new WorkflowEdgeDefinition { FromNodeId = "if_1", ToNodeId = "http_1", EdgeKey = "true" },
                new WorkflowEdgeDefinition { FromNodeId = "if_1", ToNodeId = "log_1", EdgeKey = "false" },
                new WorkflowEdgeDefinition { FromNodeId = "http_1", ToNodeId = "log_1", EdgeKey = "default" }
            ]
        };
}
