using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Infrastructure.Secrets;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowSecretService(
    IWorkflowDomainAccessor domain,
    IWorkflowSecretRepository secrets,
    IWorkflowSecretProtector protector) : IWorkflowSecretService
{
    public async Task<WorkflowSecretSummary> UpsertAsync(CreateWorkflowSecretRequest request, CancellationToken cancellationToken = default)
    {
        if (!protector.IsConfigured)
            throw new InvalidOperationException("Secret encryption is not configured (Secrets.EncryptionKeyBase64).");

        var ctx = domain.GetRequiredDomain();
        var key = request.Key.Trim();
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Secret key is required.");

        var existing = await secrets.GetByKeyAsync(ctx.DomainName, key, cancellationToken);
        var now = DateTime.UtcNow;
        var encrypted = protector.Protect(request.Value);

        if (existing == null)
        {
            var doc = new WorkflowSecretDocument
            {
                DomainId = ctx.DomainId,
                DomainName = ctx.DomainName,
                Key = key,
                EncryptedValue = encrypted,
                CreatedAt = now,
                UpdatedAt = now
            };
            await secrets.InsertAsync(doc, cancellationToken);
            return Map(doc);
        }

        existing.EncryptedValue = encrypted;
        existing.UpdatedAt = now;
        await secrets.ReplaceAsync(existing, cancellationToken);
        return Map(existing);
    }

    public async Task<IReadOnlyList<WorkflowSecretSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var items = await secrets.ListAsync(ctx.DomainName, cancellationToken);
        return items.Select(Map).ToList();
    }

    private static WorkflowSecretSummary Map(WorkflowSecretDocument doc) =>
        new() { Id = doc.Id, Key = doc.Key, UpdatedAt = doc.UpdatedAt };
}
