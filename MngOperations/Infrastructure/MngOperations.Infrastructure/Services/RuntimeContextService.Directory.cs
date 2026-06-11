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

    /// <summary>Person tipi pool alan key'leri (op_fields) — metadata cache.</summary>
    private Task<IReadOnlyList<string>> GetPersonPoolFieldKeysAsync(
        string token,
        CancellationToken cancellationToken) =>
        _metadataCache.GetPersonPoolFieldKeysAsync(token, cancellationToken);

    /// <summary>Person grup tipi pool alan key'leri — metadata cache.</summary>
    private Task<IReadOnlyList<string>> GetGroupPoolFieldKeysAsync(
        string token,
        CancellationToken cancellationToken) =>
        _metadataCache.GetGroupPoolFieldKeysAsync(token, cancellationToken);

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
