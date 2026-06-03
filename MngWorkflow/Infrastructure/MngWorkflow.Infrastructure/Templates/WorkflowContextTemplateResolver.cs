using System.Globalization;
using System.Text.RegularExpressions;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Services;
using MngWorkflow.Infrastructure.Nodes;

namespace MngWorkflow.Infrastructure.Templates;

public sealed partial class WorkflowContextTemplateResolver(IWorkflowSecretResolver secretResolver) : IWorkflowContextTemplateResolver
{
    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_\-\.]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex ContextPlaceholderRegex();

    public string Resolve(WorkflowExecutionContext context, string domainName, string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var withSecrets = secretResolver.Resolve(domainName, template);
        if (!withSecrets.Contains("{{", StringComparison.Ordinal))
            return withSecrets;

        return ContextPlaceholderRegex().Replace(withSecrets, match =>
        {
            var path = match.Groups[1].Value;
            var value = ResolvePath(context, path);
            return FormatValue(value);
        });
    }

    public string? ResolveOptional(WorkflowExecutionContext context, string domainName, string? template) =>
        string.IsNullOrWhiteSpace(template) ? null : Resolve(context, domainName, template.Trim());

    private static object? ResolvePath(WorkflowExecutionContext context, string path) =>
        path switch
        {
            "instance.id" => context.InstanceId,
            "instance.correlationId" => context.CorrelationId,
            "instance.workflowVersionId" => context.WorkflowVersionId,
            "instance.domainId" => context.DomainId,
            "instance.domainName" => context.DomainName,
            _ when path.StartsWith("event.", StringComparison.Ordinal)
                || path.StartsWith("variables.", StringComparison.Ordinal)
                || path.StartsWith("outputs.", StringComparison.Ordinal)
                => ContextPathReader.Read(context, path),
            _ => null
        };

    private static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
}
