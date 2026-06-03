using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Infrastructure.Secrets;

public sealed partial class WorkflowSecretResolver(IServiceScopeFactory scopeFactory) : IWorkflowSecretResolver
{
    [GeneratedRegex(@"\{\{\s*secrets\.([a-zA-Z0-9_\-\.]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex SecretPlaceholderRegex();

    public string Resolve(string domainName, string template)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
            return template;

        return SecretPlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            using var scope = scopeFactory.CreateScope();
            var secrets = scope.ServiceProvider.GetRequiredService<IWorkflowSecretRepository>();
            var protector = scope.ServiceProvider.GetRequiredService<IWorkflowSecretProtector>();

            var doc = secrets.GetByKeyAsync(domainName, key).GetAwaiter().GetResult();
            if (doc == null)
                throw new InvalidOperationException($"Secret '{key}' not found.");

            return protector.Unprotect(doc.EncryptedValue);
        });
    }
}
