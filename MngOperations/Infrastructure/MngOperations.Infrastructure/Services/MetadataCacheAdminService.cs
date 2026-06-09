using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Permissions;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class MetadataCacheAdminService : IMetadataCacheAdminService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IMetadataCache _metadataCache;
    private readonly IPermissionEvaluator _permissions;
    private readonly IRequestContext _requestContext;

    public MetadataCacheAdminService(
        IMngDataGatewayClient dg,
        IMetadataCache metadataCache,
        IPermissionEvaluator permissions,
        IRequestContext requestContext)
    {
        _dg = dg;
        _metadataCache = metadataCache;
        _permissions = permissions;
        _requestContext = requestContext;
    }

    public async Task<MetadataCacheReloadResult> ReloadWorkspaceAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new OperationCoreException(
                "WORKSPACE_ID_REQUIRED",
                "workspaceId is required.",
                "workspaceId zorunludur.",
                400);
        }

        var token = _requestContext.BearerToken
            ?? throw new InvalidOperationException("Bearer token is required for metadata cache reload.");

        var wsId = workspaceId.Trim();
        var workspace = await _dg.GetByIdAsync<WorkspaceRecord>(
            OcDatasets.Workspaces,
            wsId,
            token,
            cancellationToken);

        if (workspace == null)
        {
            throw new OperationCoreException(
                "WORKSPACE_NOT_FOUND",
                $"Workspace '{wsId}' not found.",
                $"Workspace '{wsId}' bulunamadı.",
                404);
        }

        _permissions.EnsureWorkspace(workspace, WorkspaceAction.Admin);

        return await _metadataCache.ReloadWorkspaceAsync(wsId, token, cancellationToken);
    }
}
