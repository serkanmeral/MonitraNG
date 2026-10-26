using System.Net;
using System.Text.Json;
using MngOperations.Application.Contracts.WorkspaceFolders;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class WorkspaceFolderService : IWorkspaceFolderService
{
    public const int MaxDepth = 8;
    private const int PageSize = 200;
    private const int MaxRows = 5000;

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _requestContext;

    public WorkspaceFolderService(IMngDataGatewayClient dg, IRequestContext requestContext)
    {
        _dg = dg;
        _requestContext = requestContext;
    }

    public async Task<IReadOnlyList<WorkspaceFolderDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var folders = await LoadFoldersAsync(token, cancellationToken);
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return folders.OrderBy(f => f.SortOrder).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var placements = await LoadWorkspacePlacementsAsync(token, cancellationToken);
        var byId = folders.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var include = new HashSet<string>(StringComparer.Ordinal);

        foreach (var folder in folders)
        {
            if (GroupListParser.Intersects(_requestContext.UserGroups, folder.ViewGroups))
                include.Add(folder.Id);
        }

        foreach (var folderId in placements.Select(p => p.FolderId).Where(id => !string.IsNullOrEmpty(id)))
            AddAncestors(folderId!, byId, include);

        return folders
            .Where(f => include.Contains(f.Id))
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<WorkspaceFolderDto> CreateAsync(CreateWorkspaceFolderRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var token = RequireToken();
        var name = RequireName(request.Name);
        var parentId = EmptyToNull(request.ParentFolderId);
        var viewGroups = NormalizeGroups(request.ViewGroups);

        var folders = await LoadFoldersAsync(token, cancellationToken);
        if (parentId != null && folders.All(f => f.Id != parentId))
            throw NotFoundFolder();

        var depth = parentId == null ? 1 : Depth(parentId, folders) + 1;
        if (depth > MaxDepth)
            throw TooDeep();

        EnsureUniqueName(folders, parentId, name, exceptId: null);

        var sort = folders.Where(f => SameParent(f.ParentFolderId, parentId)).Select(f => f.SortOrder).DefaultIfEmpty(0).Max() + 1;
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["viewGroups"] = viewGroups,
            ["sortOrder"] = sort,
        };
        if (parentId != null)
            payload["parentFolderId"] = parentId;

        var created = await WriteAsync(
            () => _dg.CreateAsync(OcDatasets.WorkspaceFolders, payload, token, cancellationToken),
            cancellationToken);
        return MapFolder(created);
    }

    public async Task<WorkspaceFolderDto> UpdateAsync(string id, UpdateWorkspaceFolderRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var token = RequireToken();
        var folderId = RequireId(id);
        var name = RequireName(request.Name);
        var viewGroups = NormalizeGroups(request.ViewGroups);

        var folders = await LoadFoldersAsync(token, cancellationToken);
        var current = folders.FirstOrDefault(f => f.Id == folderId) ?? throw NotFoundFolder();
        EnsureUniqueName(folders, current.ParentFolderId, name, folderId);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["viewGroups"] = viewGroups,
        };
        var updated = await WriteAsync(
            () => _dg.UpdateAsync(OcDatasets.WorkspaceFolders, folderId, payload, token, cancellationToken),
            cancellationToken);
        return MapFolder(updated);
    }

    public async Task<WorkspaceFolderDto> MoveAsync(string id, MoveWorkspaceFolderRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var token = RequireToken();
        var folderId = RequireId(id);
        var parentId = EmptyToNull(request.ParentFolderId);

        var folders = await LoadFoldersAsync(token, cancellationToken);
        var current = folders.FirstOrDefault(f => f.Id == folderId) ?? throw NotFoundFolder();
        if (parentId != null && folders.All(f => f.Id != parentId))
            throw NotFoundFolder();
        if (parentId != null && (parentId == folderId || IsDescendant(parentId, folderId, folders)))
        {
            throw new OperationCoreException(
                "FOLDER_CYCLE",
                "A folder cannot be moved into itself or one of its descendants.",
                "Klasör kendi altına veya altındaki bir klasöre taşınamaz.",
                400);
        }

        var newDepth = parentId == null ? 1 : Depth(parentId, folders) + 1;
        var extra = SubtreeExtraDepth(folderId, folders);
        if (newDepth + extra > MaxDepth)
            throw TooDeep();

        EnsureUniqueName(folders, parentId, current.Name, folderId);

        var payload = new Dictionary<string, object?> { ["parentFolderId"] = parentId };
        var updated = await WriteAsync(
            () => _dg.UpdateAsync(OcDatasets.WorkspaceFolders, folderId, payload, token, cancellationToken),
            cancellationToken);
        return MapFolder(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var token = RequireToken();
        var folderId = RequireId(id);
        var folders = await LoadFoldersAsync(token, cancellationToken);
        if (folders.All(f => f.Id != folderId))
            throw NotFoundFolder();
        if (folders.Any(f => f.ParentFolderId == folderId))
            throw NotEmpty();

        var placements = await LoadWorkspacePlacementsAsync(token, cancellationToken);
        if (placements.Any(p => p.FolderId == folderId))
            throw NotEmpty();

        try
        {
            var deleted = await _dg.DeleteAsync(OcDatasets.WorkspaceFolders, folderId, token, cancellationToken);
            if (!deleted)
                throw NotFoundFolder();
        }
        catch (HttpRequestException ex)
        {
            throw StorageError(ex);
        }
    }

    public async Task MoveWorkspaceAsync(string workspaceId, MoveWorkspaceToFolderRequest request, CancellationToken cancellationToken = default)
    {
        EnsureManager();
        var token = RequireToken();
        var id = RequireId(workspaceId);
        var folderId = EmptyToNull(request.FolderId);

        var workspace = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.Workspaces, id, token, cancellationToken, expand: false);
        if (workspace == null || string.IsNullOrEmpty(ReadId(workspace)))
            throw new OperationCoreException("WORKSPACE_NOT_FOUND", "Workspace was not found.", "Workspace bulunamadı.", 404);

        if (folderId != null)
        {
            var folders = await LoadFoldersAsync(token, cancellationToken);
            if (folders.All(f => f.Id != folderId))
                throw NotFoundFolder();
        }

        var payload = new Dictionary<string, object?> { ["folderId"] = folderId };
        await WriteAsync(
            () => _dg.UpdateAsync(OcDatasets.Workspaces, id, payload, token, cancellationToken),
            cancellationToken);
    }

    private async Task<List<WorkspaceFolderDto>> LoadFoldersAsync(string token, CancellationToken cancellationToken)
    {
        var rows = await ListAllAsync(OcDatasets.WorkspaceFolders, token, cancellationToken);
        return rows.Select(MapFolder).Where(f => f.Id.Length > 0 && f.Name.Length > 0).ToList();
    }

    private async Task<List<WorkspacePlacement>> LoadWorkspacePlacementsAsync(string token, CancellationToken cancellationToken)
    {
        var rows = await ListAllAsync(OcDatasets.Workspaces, token, cancellationToken);
        return rows
            .Select(row => new WorkspacePlacement(ReadId(row), ReadString(row, "folderId")))
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .ToList();
    }

    private async Task<List<Dictionary<string, object?>>> ListAllAsync(string dataset, string token, CancellationToken cancellationToken)
    {
        var all = new List<Dictionary<string, object?>>();
        var skip = 0;
        while (skip < MaxRows)
        {
            List<Dictionary<string, object?>> page;
            try
            {
                var rows = await _dg.GetAsync<Dictionary<string, object?>>(
                    dataset,
                    $"expand=false&limit={PageSize}&skip={skip}",
                    token,
                    cancellationToken);
                page = rows.ToList();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                if (dataset == OcDatasets.WorkspaceFolders)
                    return all;
                throw StorageError(ex);
            }

            all.AddRange(page);
            if (page.Count < PageSize)
                break;
            skip += PageSize;
        }

        return all;
    }

    private async Task<Dictionary<string, object?>> WriteAsync(
        Func<Task<Dictionary<string, object?>>> call,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            return await call();
        }
        catch (HttpRequestException ex)
        {
            throw StorageError(ex);
        }
    }

    private void EnsureManager()
    {
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return;

        throw new OperationCoreException(
            "FOLDER_FORBIDDEN",
            "Only domain managers can change workspace folders.",
            "Klasör işlemleri için manager yetkisi gerekli.",
            403);
    }

    private string RequireToken()
    {
        if (string.IsNullOrEmpty(_requestContext.BearerToken))
            throw new OperationCoreException("UNAUTHORIZED", "Bearer token is required.", "Bearer token gerekli.", 401);
        return _requestContext.BearerToken;
    }

    private static string RequireId(string? id)
    {
        var value = id?.Trim();
        if (string.IsNullOrEmpty(value))
            throw new OperationCoreException("FOLDER_ID_REQUIRED", "Id is required.", "Kayıt id gerekli.", 400);
        return value;
    }

    private static string RequireName(string? name)
    {
        var value = name?.Trim() ?? "";
        if (value.Length == 0 || value.Length > 120)
            throw new OperationCoreException(
                "FOLDER_NAME_INVALID",
                "Folder name must be between 1 and 120 characters.",
                "Klasör adı 1 ile 120 karakter arasında olmalı.",
                400);
        return value;
    }

    private static List<string> NormalizeGroups(IEnumerable<string>? groups)
    {
        return (groups ?? Array.Empty<string>())
            .Select(g => g?.Trim())
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .Cast<string>()
            .ToList();
    }

    private static void EnsureUniqueName(IEnumerable<WorkspaceFolderDto> folders, string? parentId, string name, string? exceptId)
    {
        var taken = folders.Any(f =>
            f.Id != exceptId
            && SameParent(f.ParentFolderId, parentId)
            && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (!taken)
            return;

        throw new OperationCoreException(
            "FOLDER_NAME_TAKEN",
            "A folder with this name already exists here.",
            "Bu konumda aynı adlı bir klasör var.",
            409);
    }

    private static bool SameParent(string? left, string? right) =>
        string.Equals(left ?? "", right ?? "", StringComparison.Ordinal);

    private static int Depth(string id, IReadOnlyList<WorkspaceFolderDto> folders)
    {
        var byId = folders.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var depth = 1;
        var seen = new HashSet<string>(StringComparer.Ordinal) { id };
        if (!byId.TryGetValue(id, out var current))
            return depth;

        var parentId = current.ParentFolderId;
        while (!string.IsNullOrEmpty(parentId))
        {
            if (!seen.Add(parentId))
                break;
            depth++;
            if (!byId.TryGetValue(parentId, out var parent))
                break;
            parentId = parent.ParentFolderId;
        }

        return depth;
    }

    private static int SubtreeExtraDepth(string id, IReadOnlyList<WorkspaceFolderDto> folders)
    {
        var children = folders.Where(f => f.ParentFolderId == id).Select(f => f.Id).ToList();
        if (children.Count == 0)
            return 0;
        return 1 + children.Max(child => SubtreeExtraDepth(child, folders));
    }

    private static bool IsDescendant(string candidateId, string ancestorId, IReadOnlyList<WorkspaceFolderDto> folders)
    {
        var byId = folders.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = candidateId;
        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            if (!byId.TryGetValue(current, out var folder))
                return false;
            if (folder.ParentFolderId == ancestorId)
                return true;
            current = folder.ParentFolderId;
        }

        return false;
    }

    private static void AddAncestors(string folderId, IReadOnlyDictionary<string, WorkspaceFolderDto> byId, HashSet<string> include)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = folderId;
        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            include.Add(current);
            if (!byId.TryGetValue(current, out var folder))
                break;
            current = folder.ParentFolderId;
        }
    }

    private static WorkspaceFolderDto MapFolder(Dictionary<string, object?> row)
    {
        return new WorkspaceFolderDto
        {
            Id = ReadId(row) ?? "",
            Name = ReadString(row, "name") ?? "",
            ParentFolderId = ReadString(row, "parentFolderId"),
            ViewGroups = ReadGroups(row, "viewGroups"),
            SortOrder = ReadInt(row, "sortOrder"),
        };
    }

    private static string? ReadId(IReadOnlyDictionary<string, object?> row) =>
        ReadString(row, "__dataId") ?? ReadString(row, "dataId");

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!TryGet(row, key, out var value) || value is null)
            return null;

        switch (value)
        {
            case string text:
                return EmptyToNull(text);
            case JsonElement el when el.ValueKind == JsonValueKind.String:
                return EmptyToNull(el.GetString());
            case JsonElement el when el.ValueKind == JsonValueKind.Object:
                if (el.TryGetProperty("__dataId", out var dataId) && dataId.ValueKind == JsonValueKind.String)
                    return EmptyToNull(dataId.GetString());
                if (el.TryGetProperty("dataId", out var alt) && alt.ValueKind == JsonValueKind.String)
                    return EmptyToNull(alt.GetString());
                return null;
            default:
                return null;
        }
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!TryGet(row, key, out var value) || value is null)
            return 0;
        return value switch
        {
            int n => n,
            long n => (int)n,
            double n => (int)n,
            JsonElement el when el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var parsed) => parsed,
            JsonElement el when el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var fromText) => fromText,
            _ => 0
        };
    }

    private static IReadOnlyList<string> ReadGroups(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!TryGet(row, key, out var value) || value is null)
            return Array.Empty<string>();

        if (value is JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (var item in el.EnumerateArray())
            {
                var id = item.ValueKind switch
                {
                    JsonValueKind.String => item.GetString(),
                    JsonValueKind.Object when item.TryGetProperty("__dataId", out var dataId) => dataId.GetString(),
                    JsonValueKind.Object when item.TryGetProperty("dataId", out var alt) => alt.GetString(),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(id))
                    list.Add(id.Trim());
            }

            return list;
        }

        if (value is IEnumerable<string> strings)
            return strings.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();

        return Array.Empty<string>();
    }

    private static bool TryGet(IReadOnlyDictionary<string, object?> row, string key, out object? value)
    {
        if (row.TryGetValue(key, out value))
            return true;
        var match = row.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(match.Key))
        {
            value = match.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static OperationCoreException NotFoundFolder() =>
        new("FOLDER_NOT_FOUND", "Folder was not found.", "Klasör bulunamadı.", 404);

    private static OperationCoreException NotEmpty() =>
        new(
            "FOLDER_NOT_EMPTY",
            "The folder still contains folders or workspaces.",
            "Klasörün içinde klasör veya workspace varken silinemez.",
            409);

    private static OperationCoreException TooDeep() =>
        new(
            "FOLDER_TOO_DEEP",
            $"Folder nesting cannot exceed {MaxDepth} levels.",
            $"Klasör derinliği {MaxDepth} seviyeyi geçemez.",
            400);

    private static OperationCoreException StorageError(HttpRequestException ex)
    {
        var status = (int?)ex.StatusCode ?? 502;
        return new OperationCoreException(
            "FOLDER_STORAGE",
            ex.Message,
            "Klasör kaydı yazılamadı. op_workspace_folders dataset'i bu domainde yoksa önce oluşturulmalı.",
            status is >= 400 and < 600 ? status : 502);
    }

    private sealed record WorkspacePlacement(string? Id, string? FolderId);
}
