using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Infrastructure.Clients;
using MngWorkflow.Infrastructure.Engine;
using MngWorkflow.Infrastructure.Expressions;
using MngWorkflow.Infrastructure.Http;
using MngWorkflow.Infrastructure.Messaging;
using MngWorkflow.Infrastructure.Nodes;
using MngWorkflow.Infrastructure.Persistence;
using MngWorkflow.Infrastructure.Persistence.Repositories;
using MngWorkflow.Infrastructure.Secrets;
using MngWorkflow.Infrastructure.Services;

namespace MngWorkflow.Infrastructure;

public sealed class WorkflowNodeRegistry : INodeRegistry
{
    private readonly IReadOnlyDictionary<string, IWorkflowNode> _nodes;

    public WorkflowNodeRegistry(IEnumerable<IWorkflowNode> nodes) =>
        _nodes = nodes.ToDictionary(n => n.NodeType, StringComparer.Ordinal);

    public IWorkflowNode Resolve(string nodeType) =>
        _nodes.TryGetValue(nodeType, out var node)
            ? node
            : throw new InvalidOperationException($"Unknown node type: {nodeType}");
}

public static class WorkflowEngineServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowEngineCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWorkflowMongo(configuration);

        services.AddSingleton<WorkflowRabbitMqConnectionManager>();
        services.AddSingleton<WorkflowTopologyBootstrapper>();
        services.AddSingleton<WorkflowEventTopologyBootstrapper>();
        services.AddSingleton<IWorkflowQueuePublisher, WorkflowQueuePublisher>();

        services.AddSingleton<IWorkflowExpressionEvaluator, JintWorkflowExpressionEvaluator>();

        services.AddSingleton<IWorkflowNode, ManualTriggerNode>();
        services.AddSingleton<IWorkflowNode, IfNode>();
        services.AddSingleton<IWorkflowNode, HttpRequestNode>();
        services.AddSingleton<IWorkflowNode, WriteLogNode>();
        services.AddSingleton<IWorkflowNode, ApprovalWaitNode>();
        services.AddSingleton<IWorkflowNode, DelayWaitNode>();
        services.AddSingleton<IWorkflowNode, CreateWorkItemNode>();
        services.AddSingleton<IWorkflowNode, ApplyTransitionWorkItemNode>();
        services.AddSingleton<IWorkflowNode, UpdateWorkItemNode>();
        services.AddSingleton<IWorkflowNode, ParallelForkNode>();
        services.AddSingleton<IWorkflowNode, ParallelJoinNode>();
        services.AddSingleton<IWorkflowNode, EngineCommandNode>();
        services.AddSingleton<IWorkflowNode, BlockIpNode>();
        services.AddSingleton<INodeRegistry, WorkflowNodeRegistry>();

        services.AddSingleton<IWorkflowSecretProtector, AesWorkflowSecretProtector>();
        services.AddSingleton<IWorkflowSecretResolver, WorkflowSecretResolver>();
        services.AddSingleton<IWorkflowContextTemplateResolver, Templates.WorkflowContextTemplateResolver>();

        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IWorkflowVersionRepository, WorkflowVersionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<INodeExecutionRepository, NodeExecutionRepository>();
        services.AddScoped<IWorkflowTriggerRepository, WorkflowTriggerRepository>();
        services.AddScoped<IWorkflowApprovalRepository, WorkflowApprovalRepository>();
        services.AddScoped<IWorkflowSecretRepository, WorkflowSecretRepository>();
        services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
        services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
        services.AddScoped<IWorkflowVersionService, WorkflowVersionService>();
        services.AddScoped<IWorkflowRunService, WorkflowRunService>();
        services.AddScoped<IWorkflowTriggerSyncService, WorkflowTriggerSyncService>();
        services.AddScoped<IWorkflowEventTriggerProcessor, WorkflowEventTriggerProcessor>();
        services.AddScoped<IWorkflowApprovalService, WorkflowApprovalService>();
        services.AddScoped<IWorkflowResumeService, WorkflowResumeService>();
        services.AddScoped<IWorkflowScheduleSyncService, WorkflowScheduleSyncService>();
        services.AddScoped<IWorkflowHookService, WorkflowHookService>();
        services.AddScoped<IWorkflowSecretService, WorkflowSecretService>();

        services.AddHttpClient("MngScheduler");
        services.AddHttpClient("MngKeeperAuth");
        services.AddHttpClient("MngOperations");
        services.AddScoped<IWorkflowSchedulerClient, WorkflowSchedulerClient>();
        services.AddScoped<IWorkflowKeeperAuthClient, WorkflowKeeperAuthClient>();
        services.AddScoped<IWorkflowOperationsClient, WorkflowOperationsClient>();
        services.AddHttpContextAccessor();
        services.AddScoped<IWorkflowDomainAccessor, WorkflowDomainAccessor>();

        services.AddHttpClient("workflow-http");
        services.AddHttpClient("MngReactor");

        return services;
    }

    public static IServiceCollection AddWorkflowWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWorkflowEngineCore(configuration);
        services.AddHostedService<WorkflowExecutionConsumer>();
        services.AddHostedService<WorkflowResumeConsumer>();
        services.AddHostedService<WorkflowEventTriggerConsumer>();
        return services;
    }
}
