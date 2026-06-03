using MngWorkflow.Application.Execution;
using MngWorkflow.Infrastructure.Templates;
using Xunit;

namespace MngWorkflow.Tests;

public sealed class WorkflowContextTemplateResolverTests
{
    private static WorkflowExecutionContext BuildContext() => new()
    {
        InstanceId = "inst-1",
        WorkflowVersionId = "ver-1",
        DomainId = "odak",
        DomainName = "odak",
        CorrelationId = "corr-abc",
        Event = new Dictionary<string, object?> { ["severity"] = "critical" },
        Variables = new Dictionary<string, object?>(),
        Outputs = new Dictionary<string, object?>
        {
            ["create_1"] = new Dictionary<string, object?>
            {
                ["workItemId"] = "wi-99"
            }
        }
    };

    [Fact]
    public void Resolve_replaces_instance_and_output_paths()
    {
        var resolver = new WorkflowContextTemplateResolver(new NoOpSecretResolver());
        var ctx = BuildContext();

        var result = resolver.Resolve(ctx, "odak", "WI {{instance.correlationId}} / {{outputs.create_1.workItemId}} / {{event.severity}}");
        Assert.Equal("WI corr-abc / wi-99 / critical", result);
    }

    [Fact]
    public void ResolveOptional_returns_null_for_blank()
    {
        var resolver = new WorkflowContextTemplateResolver(new NoOpSecretResolver());
        Assert.Null(resolver.ResolveOptional(BuildContext(), "odak", "  "));
    }

    private sealed class NoOpSecretResolver : Application.Services.IWorkflowSecretResolver
    {
        public string Resolve(string domainName, string template) => template;
    }
}
