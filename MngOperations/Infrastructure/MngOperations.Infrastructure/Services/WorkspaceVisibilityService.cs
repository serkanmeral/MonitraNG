using System.Net;
using System.Text.Json;
using MngOperations.Application.Contracts.Workspaces;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class WorkspaceVisibilityService : IWorkspaceVisibilityService
{
    private const int PageSize = 200;
    private const int MaxRows = 5000;

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _requestContext;
    private readonly IPermissionEvaluator _permissions;

    public WorkspaceVisibilityService(
        IMngDataGatewayClient dg,
        IRequestContext requestContext,
        IPermissionEvaluator permissions)
    {
        _dg = dg;
        _requestContext = requestContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<VisibleWorkspaceItem>> ListVisibleAsync(CancellationToken cancellationToken = default)
    {
        var token = _requestContext.BearerToken;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Bearer token is required.");

        var rows = await ListAllAsync(token, cancellationToken);
        var visible = new List<VisibleWorkspaceItem>();
        foreach (var row in rows)
        {
            var record = ToRecord(row);
            if (string.IsNullOrWhiteSpace(record.DataId) || string.IsNullOrWhiteSpace(record.Name))
                continue;
            if (!_permissions.CanSeeWorkspace(record))
                continue;

            visible.Add(new VisibleWorkspaceItem
            {
                DataId = record.DataId,
                Name = record.Name,
                Description = ReadString(row, "description"),
                WorkspaceType = ReadString(row, "workspaceType"),
                WorkItemKeyPrefix = record.WorkItemKeyPrefix,
                FolderId = ReadString(row, "folderId"),
                DiFolderId = ReadString(row, "diFolderId"),
            });
        }

        return visible
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<Dictionary<string, object?>>> ListAllAsync(string token, CancellationToken cancellationToken)
    {
        var all = new List<Dictionary<string, object?>>();
        var skip = 0;
        while (skip < MaxRows)
        {
            List<Dictionary<string, object?>> page;
            try
            {
                var rows = await _dg.GetAsync<Dictionary<string, object?>>(
                    OcDatasets.Workspaces,
                    $"expand=false&limit={PageSize}&skip={skip}",
                    token,
                    cancellationToken);
                page = rows.ToList();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return all;
            }

            all.AddRange(page);
            if (page.Count < PageSize)
                break;
            skip += PageSize;
        }

        return all;
    }

    private static WorkspaceRecord ToRecord(IReadOnlyDictionary<string, object?> row)
    {
        return new WorkspaceRecord
        {
            DataId = ReadString(row, "__dataId") ?? ReadString(row, "dataId"),
            Name = ReadString(row, "name"),
            WorkItemKeyPrefix = ReadString(row, "workItemKeyPrefix"),
            ViewGroups = ToElement(row, "viewGroups"),
            EditGroups = ToElement(row, "editGroups"),
            AdminGroups = ToElement(row, "adminGroups"),
            OwnerGroups = ToElement(row, "ownerGroups"),
        };
    }

    private static JsonElement? ToElement(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
            return null;
        if (value is JsonElement element)
            return element.ValueKind == JsonValueKind.Null ? null : element;
        return JsonSerializer.SerializeToElement(value);
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
            return null;

        switch (value)
        {
            case string text:
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            case JsonElement el when el.ValueKind == JsonValueKind.String:
                var s = el.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            case JsonElement el when el.ValueKind == JsonValueKind.Object:
                if (el.TryGetProperty("__dataId", out var dataId) && dataId.ValueKind == JsonValueKind.String)
                    return string.IsNullOrWhiteSpace(dataId.GetString()) ? null : dataId.GetString()!.Trim();
                return null;
            default:
                return null;
        }
    }
}
