using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public partial class RuntimeContextService
{
    private async Task<IReadOnlyDictionary<string, PersonDisplayDto>> ResolvePeopleForCardsAsync(
        IReadOnlyList<WorkItemCardDto> cards,
        string token,
        CancellationToken cancellationToken)
    {
        if (cards.Count == 0)
            return new Dictionary<string, PersonDisplayDto>();

        var personPoolKeys = await GetPersonPoolFieldKeysAsync(token, cancellationToken);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var card in cards)
        {
            AddPersonId(ids, card.Assignee);
            AddPersonId(ids, card.CreatedBy);

            if (card.Fields is not { ValueKind: JsonValueKind.Object } fields)
                continue;

            foreach (var key in personPoolKeys)
            {
                if (fields.TryGetProperty(key, out var value))
                    AddPersonIdsFromElement(ids, value);
            }

            // watchers gibi çekirdek çoklu person alanları extraFields dışında da olabilir.
            if (fields.TryGetProperty("watchers", out var watchers))
                AddPersonIdsFromElement(ids, watchers);
        }

        if (ids.Count == 0)
            return new Dictionary<string, PersonDisplayDto>();

        return await _personDirectory.GetPeopleAsync(ids, token, cancellationToken);
    }

    /// <summary>Person tipi pool alan key'leri (op_fields, fieldType ∈ persons/person) — cache'li.</summary>
    private async Task<IReadOnlyList<string>> GetPersonPoolFieldKeysAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var keys = new List<string>(CorePersonFieldKeys);
        try
        {
            var fields = await _metadataCache.GetCatalogListAsync(OcDatasets.Fields, token, cancellationToken);
            foreach (var field in fields)
            {
                var fieldType = WorkItemDataHelper.GetString(field, "fieldType")?.Trim().ToLowerInvariant();
                if (fieldType is not ("persons" or "person"))
                    continue;

                var key = WorkItemDataHelper.GetString(field, "key");
                if (!string.IsNullOrWhiteSpace(key) && !keys.Contains(key))
                    keys.Add(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Person pool field keys resolve failed; using core keys only.");
        }

        return keys;
    }

    /// <summary>Person grup tipi pool alan key'leri (op_fields, fieldType ∈ personGroups/personGroup/group) — cache'li.</summary>
    private async Task<IReadOnlyList<string>> GetGroupPoolFieldKeysAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var keys = new List<string>(CoreGroupFieldKeys);
        try
        {
            var fields = await _metadataCache.GetCatalogListAsync(OcDatasets.Fields, token, cancellationToken);
            foreach (var field in fields)
            {
                var fieldType = WorkItemDataHelper.GetString(field, "fieldType")?.Trim().ToLowerInvariant();
                if (fieldType is not ("persongroups" or "persongroup" or "group"))
                    continue;

                var key = WorkItemDataHelper.GetString(field, "key");
                if (!string.IsNullOrWhiteSpace(key) && !keys.Contains(key))
                    keys.Add(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Group pool field keys resolve failed; using core keys only.");
        }

        return keys;
    }

    /// <summary>
    /// Sayfadaki kartların grup alanlarından (assignmentGroups + personGroups tipi pool alanlar) id'leri toplar
    /// ve Keeper cache'inden id → grup adı map'ini döner. <see cref="ResolvePeopleForCardsAsync"/> ile aynı desen.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, PersonDisplayDto>> ResolveGroupsForCardsAsync(
        IReadOnlyList<WorkItemCardDto> cards,
        string token,
        CancellationToken cancellationToken)
    {
        if (cards.Count == 0)
            return new Dictionary<string, PersonDisplayDto>();

        var groupPoolKeys = await GetGroupPoolFieldKeysAsync(token, cancellationToken);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var card in cards)
        {
            if (card.Fields is not { ValueKind: JsonValueKind.Object } fields)
                continue;

            foreach (var key in groupPoolKeys)
            {
                if (fields.TryGetProperty(key, out var value))
                    AddPersonIdsFromElement(ids, value);
            }
        }

        if (ids.Count == 0)
            return new Dictionary<string, PersonDisplayDto>();

        return await _groupDirectory.GetGroupsAsync(ids, token, cancellationToken);
    }

    private static void AddPersonId(HashSet<string> ids, string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            ids.Add(id.Trim());
    }

    private static void AddPersonIdsFromElement(HashSet<string> ids, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                AddPersonId(ids, value.GetString());
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                    AddPersonIdsFromElement(ids, item);
                break;
            case JsonValueKind.Object:
                // İlişki nesnesi olarak gelmişse id alanını dene.
                if (value.TryGetProperty("__dataId", out var dataId) && dataId.ValueKind == JsonValueKind.String)
                    AddPersonId(ids, dataId.GetString());
                else if (value.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    AddPersonId(ids, idProp.GetString());
                break;
        }
    }
}
