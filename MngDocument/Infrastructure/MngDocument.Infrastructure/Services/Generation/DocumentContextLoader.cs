using System.Text.Json.Nodes;
using MngDocument.Application.Interfaces;
using MngDocument.Infrastructure.Services.Generation;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DocumentContextLoader
{
    private readonly IMngDataGatewayClient _dg;

    public DocumentContextLoader(IMngDataGatewayClient dg)
    {
        _dg = dg;
    }

    public async Task<JsonObject> LoadAsync(
        DocumentContextTypeDefinition definition,
        string rootId,
        string? token,
        CancellationToken ct)
    {
        var rootRecord = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            definition.RootDataset,
            rootId.Trim(),
            token,
            ct);

        if (rootRecord is null)
            throw new KeyNotFoundException($"Context root not found: {definition.RootDataset}/{rootId}");

        var tree = DocumentContextPathResolver.ToJsonObject(rootRecord);

        foreach (var relation in definition.Relations)
        {
            var relationId = DocumentContextPathResolver.ExtractRelationId(
                DocumentContextPathResolver.GetNode(tree, relation.Path));

            if (string.IsNullOrWhiteSpace(relationId))
            {
                if (!relation.Optional)
                {
                    // Try loading from unexpanded id string on parent path last segment
                    var parentPath = relation.Path.Contains('.')
                        ? relation.Path[..relation.Path.LastIndexOf('.')]
                        : string.Empty;
                    var leaf = relation.Path.Contains('.')
                        ? relation.Path[(relation.Path.LastIndexOf('.') + 1)..]
                        : relation.Path;
                    var parent = string.IsNullOrEmpty(parentPath)
                        ? tree
                        : DocumentContextPathResolver.GetNode(tree, parentPath) as JsonObject;
                    if (parent is not null && parent.TryGetPropertyValue(leaf, out var rawId))
                        relationId = rawId?.ToString()?.Trim();
                }

                if (string.IsNullOrWhiteSpace(relationId))
                    continue;
            }

            var related = await _dg.GetByIdAsync<Dictionary<string, object?>>(
                relation.Dataset,
                relationId,
                token,
                ct);

            if (related is null)
                continue;

            DocumentContextPathResolver.SetAtPath(
                tree,
                relation.Path,
                DocumentContextPathResolver.ToJsonObject(related));
        }

        return tree;
    }
}
