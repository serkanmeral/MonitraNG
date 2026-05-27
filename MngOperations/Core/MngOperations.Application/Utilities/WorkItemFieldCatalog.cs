using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;

namespace MngOperations.Application.Utilities;

public static class WorkItemFieldCatalog
{
    public static async Task<IReadOnlyDictionary<string, FieldRecord>> BuildEnabledPoolFieldsByKeyAsync(
        WorkspaceRecord workspace,
        IMetadataCache metadataCache,
        string token,
        CancellationToken cancellationToken)
    {
        var workspaceId = workspace.DataId
            ?? throw new InvalidOperationException("Workspace record must have __dataId.");

        var enabledIds = MetadataRelationHelper.ParseIdList(workspace.EnabledFieldIds);
        var byKey = new Dictionary<string, FieldRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var fieldId in enabledIds)
        {
            var field = await metadataCache.GetFieldAsync(fieldId, token, cancellationToken);
            if (!IsPoolFieldForWorkspace(field, workspaceId))
                continue;

            if (string.IsNullOrWhiteSpace(field.Key))
                continue;

            byKey[field.Key] = field;
        }

        return byKey;
    }

    public static bool IsPoolFieldForWorkspace(FieldRecord field, string workspaceId)
    {
        if (string.Equals(field.Scope, "core", StringComparison.OrdinalIgnoreCase))
            return false;

        var fieldWorkspaceId = field.WorkspaceId;
        if (string.IsNullOrWhiteSpace(fieldWorkspaceId))
            return true;

        return string.Equals(fieldWorkspaceId, workspaceId, StringComparison.Ordinal);
    }
}
