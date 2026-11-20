using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MngExtensions.Application.Configuration;
using MngExtensions.Application.Pdks;
using MngExtensions.Domain;

namespace MngExtensions.Infrastructure.Pdks;

public sealed class PdksDataGateway : IPdksDataGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions PatchOptions = new(JsonOptions)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<MngExtensionsSettings> _settings;

    public PdksDataGateway(IHttpClientFactory httpClientFactory, IOptions<MngExtensionsSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    public async Task<DoorRecord?> FindDoorAsync(string token, string doorKey, CancellationToken cancellationToken)
    {
        var filter = $"doorKey:eq:{Uri.EscapeDataString(doorKey)}";
        var rows = await GetRowsAsync(token, $"pdks_kapilar?filter={filter}&limit=5&expand=false", cancellationToken);
        foreach (var row in rows)
        {
            var key = ReadString(row, "doorKey");
            if (!string.Equals(key, doorKey, StringComparison.Ordinal))
                continue;

            return new DoorRecord
            {
                DoorKey = key ?? doorKey,
                Title = ReadString(row, "title") ?? doorKey,
                DirectionMode = ReadString(row, "directionMode"),
                Active = ReadBool(row, "active"),
                KioskTokenHash = ReadString(row, "kioskTokenHash"),
                LocationKey = await ResolveLocationKeyAsync(token, row, cancellationToken)
            };
        }

        return null;
    }

    public async Task<PassageRecord?> FindLastAcceptedAsync(string token, string personId, CancellationToken cancellationToken)
    {
        var filter = $"personId:eq:{Uri.EscapeDataString(personId)},result:eq:accepted";
        var rows = await GetRowsAsync(
            token,
            $"pdks_gecisler?filter={filter}&sort=-occurredAt&limit=1&expand=false",
            cancellationToken);
        return rows.Count == 0 ? null : ToPassage(rows[0]);
    }

    public async Task<IReadOnlyList<PassageRecord>> ListAcceptedForPersonAsync(string token, string personId, int limit, CancellationToken cancellationToken)
    {
        var filter = $"personId:eq:{Uri.EscapeDataString(personId)},result:eq:accepted";
        var rows = await GetRowsAsync(
            token,
            $"pdks_gecisler?filter={filter}&sort=-occurredAt&limit={Math.Clamp(limit, 1, 50)}&expand=false",
            cancellationToken);
        return rows.Select(ToPassage).ToArray();
    }

    public async Task<int> ExpirePhotosAsync(string token, CancellationToken cancellationToken)
    {
        var recent = await GetRowsAsync(token, "pdks_gecisler?sort=-occurredAt&limit=100&expand=false", cancellationToken);
        foreach (var row in recent)
        {
            var state = ReadString(row, "photoState");
            var path = ReadPhotoPath(row);
            if (string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(state))
                continue;
            var id = ReadString(row, "__dataId") ?? ReadString(row, "dataId");
            var occurred = ReadDate(row, "occurredAt") ?? DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            await PatchPassageAsync(token, id, new { photoState = PassageFrame.Live, photoExpiresAt = occurred.Add(PassageFrame.Retention) }, cancellationToken);
        }

        var cutoff = DateTime.UtcNow.ToString("o");
        var filter = $"photoState:eq:{PassageFrame.Live},photoExpiresAt:lte:{cutoff}";
        var due = await GetRowsAsync(token, $"pdks_gecisler?filter={filter}&limit=20&expand=false", cancellationToken);
        var cleared = 0;
        foreach (var row in due)
        {
            var id = ReadString(row, "__dataId") ?? ReadString(row, "dataId");
            var path = ReadPhotoPath(row);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (!string.IsNullOrWhiteSpace(path))
                await DeletePhotoAsync(token, path, cancellationToken);
            await PatchPassageAsync(token, id, new { photo = (string?)null, photoState = PassageFrame.Done }, cancellationToken);
            cleared++;
        }

        return cleared;
    }

    public async Task<IReadOnlyList<PassageRecord>> ListRecentAcceptedAsync(string token, string doorKey, int limit, CancellationToken cancellationToken)
    {
        var filter = $"doorKey:eq:{Uri.EscapeDataString(doorKey)},result:eq:accepted";
        var rows = await GetRowsAsync(
            token,
            $"pdks_gecisler?filter={filter}&sort=-occurredAt&limit={limit}&expand=false",
            cancellationToken);
        return rows.Select(ToPassage).ToArray();
    }

    public async Task<GreetingSchedule> FindScheduleAsync(string token, string doorKey, CancellationToken cancellationToken)
    {
        var fallback = new GreetingSchedule();
        try
        {
            var filter = $"doorKey:eq:{Uri.EscapeDataString(doorKey)}";
            var doors = await GetRowsAsync(token, $"pdks_kapilar?filter={filter}&limit=5&expand=false", cancellationToken);
            var door = doors.FirstOrDefault(row => string.Equals(ReadString(row, "doorKey"), doorKey, StringComparison.Ordinal));
            var ayarId = door.ValueKind == JsonValueKind.Undefined ? null : ReadRelationId(door, "ayar");
            if (string.IsNullOrWhiteSpace(ayarId) && door.ValueKind != JsonValueKind.Undefined)
            {
                var locationId = ReadRelationId(door, "lokasyon");
                if (!string.IsNullOrWhiteSpace(locationId))
                {
                    var location = await GetRowAsync(token, $"pdks_lokasyonlar/{Uri.EscapeDataString(locationId)}?expand=false", cancellationToken);
                    if (location is not null)
                        ayarId = ReadRelationId(location.Value, "ayar");
                }
            }

            JsonElement? row = null;
            if (!string.IsNullOrWhiteSpace(ayarId))
                row = await GetRowAsync(token, $"pdks_ayarlar/{Uri.EscapeDataString(ayarId)}?expand=false", cancellationToken);
            if (row is null)
            {
                var matches = await GetRowsAsync(token, "pdks_ayarlar?filter=tekil:eq:ayar&limit=5&expand=false", cancellationToken);
                var match = matches.FirstOrDefault(item => string.Equals(ReadString(item, "tekil"), "ayar", StringComparison.Ordinal));
                if (match.ValueKind != JsonValueKind.Undefined)
                    row = match;
            }

            if (row is null)
                return fallback;

            var source = row.Value;
            return new GreetingSchedule
            {
                EntryClock = ReadString(source, "girisSaati") ?? fallback.EntryClock,
                ExitClock = ReadString(source, "cikisSaati") ?? fallback.ExitClock,
                ToleranceMinutes = ReadInt(source, "toleransDakika"),
                EntryPhrase = ReadString(source, "karsilamaGiris") ?? fallback.EntryPhrase,
                OnTimePhrase = ReadString(source, "karsilamaZamaninda") ?? fallback.OnTimePhrase,
                LatePhrase = ReadString(source, "karsilamaGec") ?? fallback.LatePhrase,
                ExitPhrase = ReadString(source, "karsilamaCikis") ?? fallback.ExitPhrase,
                BoardRefreshSeconds = ReadInt(source, "panoYenilemeSaniye")
            };
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public async Task WritePassageAsync(string token, PassageWrite write, CancellationToken cancellationToken)
    {
        var dg = _settings.Value.DataGateway;
        var url = $"{dg.BaseUrl.TrimEnd('/')}/api/{dg.ApiVersion}/data/pdks_gecisler";
        var client = _httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(write, options: JsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Passage write failed with status {(int)response.StatusCode}.");
    }

    public async Task<IReadOnlyList<ScreenPlace>> ListActiveLocationsAsync(string token, CancellationToken cancellationToken)
    {
        var rows = await GetRowsAsync(token, "pdks_lokasyonlar?limit=200&expand=false", cancellationToken);
        return rows
            .Where(row => ReadBool(row, "active"))
            .Select(row => new ScreenPlace
            {
                Key = ReadString(row, "locationKey") ?? string.Empty,
                Title = ReadString(row, "title") ?? ReadString(row, "locationKey") ?? string.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Title, StringComparer.CurrentCulture)
            .ToArray();
    }

    public async Task<IReadOnlyList<ScreenPlace>> ListActiveDoorsAsync(string token, string locationKey, CancellationToken cancellationToken)
    {
        var filter = $"locationKey:eq:{Uri.EscapeDataString(locationKey)}";
        var locations = await GetRowsAsync(token, $"pdks_lokasyonlar?filter={filter}&limit=5&expand=false", cancellationToken);
        var location = locations.FirstOrDefault(row => string.Equals(ReadString(row, "locationKey"), locationKey, StringComparison.Ordinal));
        if (location.ValueKind == JsonValueKind.Undefined)
            return Array.Empty<ScreenPlace>();

        var locationId = ReadString(location, "__dataId") ?? ReadString(location, "dataId");
        if (string.IsNullOrWhiteSpace(locationId))
            return Array.Empty<ScreenPlace>();

        var doors = await GetRowsAsync(token, "pdks_kapilar?limit=200&expand=false", cancellationToken);
        return doors
            .Where(row => ReadBool(row, "active") && string.Equals(ReadRelationId(row, "lokasyon"), locationId, StringComparison.Ordinal))
            .Select(row => new ScreenPlace
            {
                Key = ReadString(row, "doorKey") ?? string.Empty,
                Title = ReadString(row, "title") ?? ReadString(row, "doorKey") ?? string.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Title, StringComparer.CurrentCulture)
            .ToArray();
    }

    public async Task<BindScreenResponse?> ReplaceKioskTokenAsync(string token, string doorKey, string kioskToken, CancellationToken cancellationToken)
    {
        var filter = $"doorKey:eq:{Uri.EscapeDataString(doorKey)}";
        var rows = await GetRowsAsync(token, $"pdks_kapilar?filter={filter}&limit=5&expand=false", cancellationToken);
        var door = rows.FirstOrDefault(row => string.Equals(ReadString(row, "doorKey"), doorKey, StringComparison.Ordinal));
        if (door.ValueKind == JsonValueKind.Undefined || !ReadBool(door, "active"))
            return null;

        var id = ReadString(door, "__dataId") ?? ReadString(door, "dataId");
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var body = new Dictionary<string, object?>
        {
            ["doorKey"] = ReadString(door, "doorKey"),
            ["title"] = ReadString(door, "title"),
            ["lokasyon"] = ReadRelationId(door, "lokasyon"),
            ["directionMode"] = ReadString(door, "directionMode") ?? "auto",
            ["active"] = true,
            ["kioskTokenHash"] = kioskToken
        };
        var slug = ReadString(door, "dashboardSlug");
        if (!string.IsNullOrWhiteSpace(slug))
            body["dashboardSlug"] = slug;

        var dg = _settings.Value.DataGateway;
        var url = $"{dg.BaseUrl.TrimEnd('/')}/api/{dg.ApiVersion}/data/pdks_kapilar/{Uri.EscapeDataString(id)}";
        var client = _httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Door token update failed with status {(int)response.StatusCode}.");

        var locationId = ReadRelationId(door, "lokasyon");
        string? locationKey = null;
        string? locationTitle = null;
        if (!string.IsNullOrWhiteSpace(locationId))
        {
            var location = await GetRowAsync(token, $"pdks_lokasyonlar/{Uri.EscapeDataString(locationId)}?expand=false", cancellationToken);
            if (location is not null)
            {
                locationKey = ReadString(location.Value, "locationKey");
                locationTitle = ReadString(location.Value, "title");
            }
        }

        return new BindScreenResponse
        {
            DoorKey = doorKey,
            DoorTitle = ReadString(door, "title") ?? doorKey,
            LocationKey = locationKey,
            LocationTitle = locationTitle ?? locationKey
        };
    }

    public async Task<string?> FindDashboardSlugAsync(string token, string doorKey, CancellationToken cancellationToken)
    {
        var filter = $"doorKey:eq:{Uri.EscapeDataString(doorKey)}";
        var doors = await GetRowsAsync(token, $"pdks_kapilar?filter={filter}&limit=5&expand=false", cancellationToken);
        var door = doors.FirstOrDefault(row => string.Equals(ReadString(row, "doorKey"), doorKey, StringComparison.Ordinal));
        if (door.ValueKind == JsonValueKind.Undefined)
            return null;

        var locationId = ReadRelationId(door, "lokasyon");
        if (string.IsNullOrWhiteSpace(locationId))
            return null;

        var location = await GetRowAsync(token, $"pdks_lokasyonlar/{Uri.EscapeDataString(locationId)}?expand=false", cancellationToken);
        return location is null ? null : ReadString(location.Value, "dashboardSlug");
    }

    public async Task<int> CountAsync(string token, string dataset, string? filter, CancellationToken cancellationToken)
    {
        var query = $"{Uri.EscapeDataString(dataset)}?limit=1&skip=0&expand=false&fields=__dataId";
        if (!string.IsNullOrWhiteSpace(filter))
            query += $"&filter={Uri.EscapeDataString(filter)}";

        var dg = _settings.Value.DataGateway;
        var url = $"{dg.BaseUrl.TrimEnd('/')}/api/{dg.ApiVersion}/data/{query}";
        var client = _httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"DataGateway count failed with status {(int)response.StatusCode}.");

        if (response.Headers.TryGetValues("X-Total-Count", out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
            return Math.Max(0, total);

        return 0;
    }

    public async Task<IReadOnlyList<WorkPulseRow>> ListWorkPulseAsync(string token, CancellationToken cancellationToken)
    {
        var rows = await ListPagesAsync(token, "op_work_items", null, "workspaceId,priorityId,stateId,closedAt", cancellationToken);
        return rows.Select(row => new WorkPulseRow
        {
            WorkspaceId = ReadRelationId(row, "workspaceId"),
            PriorityId = ReadRelationId(row, "priorityId"),
            StateId = ReadRelationId(row, "stateId"),
            ClosedAt = ReadDate(row, "closedAt")
        }).ToArray();
    }

    public async Task<IReadOnlyList<PassagePulseRow>> ListPassagesSinceAsync(string token, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        // occurredAt is stored as text. A gateway gte filter compares it as a BSON date and drops every row.
        var since = sinceUtc.ToUniversalTime();
        var rows = await ListPagesAsync(token, "pdks_gecisler", "result:eq:accepted", "personId,displayName,fullName,direction,locationKey,doorKey,sinif,occurredAt", cancellationToken);
        return rows.Select(row => new PassagePulseRow
        {
            PersonId = ReadString(row, "personId"),
            Name = ReadString(row, "fullName") ?? ReadString(row, "displayName"),
            Direction = ReadString(row, "direction"),
            LocationKey = ReadString(row, "locationKey"),
            DoorKey = ReadString(row, "doorKey"),
            Sinif = ReadString(row, "sinif"),
            OccurredAt = ReadDate(row, "occurredAt")
        }).Where(row => row.OccurredAt is not null && row.OccurredAt.Value.ToUniversalTime() >= since).ToArray();
    }

    public async Task<DoorClockBook> ListDoorClocksAsync(string token, CancellationToken cancellationToken)
    {
        var fallback = new AttendanceClock("09:00", "18:00", 15);
        var settings = await ListPagesAsync(token, "pdks_ayarlar", null, "__dataId,tekil,girisSaati,cikisSaati,toleransDakika", cancellationToken);
        var clocks = new Dictionary<string, AttendanceClock>(StringComparer.Ordinal);
        foreach (var row in settings)
        {
            var id = ReadString(row, "__dataId");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            clocks[id] = ReadClock(row, fallback);
            if (string.Equals(ReadString(row, "tekil"), "ayar", StringComparison.Ordinal))
                fallback = clocks[id];
        }

        var locations = await ListPagesAsync(token, "pdks_lokasyonlar", null, "__dataId,ayar", cancellationToken);
        var locationClock = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in locations)
        {
            var id = ReadString(row, "__dataId");
            var ayar = ReadRelationId(row, "ayar");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(ayar))
                locationClock[id] = ayar;
        }

        var doors = await ListPagesAsync(token, "pdks_kapilar", null, "doorKey,ayar,lokasyon", cancellationToken);
        var byDoor = new Dictionary<string, AttendanceClock>(StringComparer.Ordinal);
        foreach (var row in doors)
        {
            var doorKey = ReadString(row, "doorKey");
            if (string.IsNullOrWhiteSpace(doorKey))
                continue;
            var ayar = ReadRelationId(row, "ayar");
            if (string.IsNullOrWhiteSpace(ayar))
            {
                var locationId = ReadRelationId(row, "lokasyon");
                if (!string.IsNullOrWhiteSpace(locationId))
                    locationClock.TryGetValue(locationId, out ayar);
            }

            byDoor[doorKey] = !string.IsNullOrWhiteSpace(ayar) && clocks.TryGetValue(ayar, out var clock) ? clock : fallback;
        }

        return new DoorClockBook { Fallback = fallback, ByDoor = byDoor };
    }

    public async Task<IReadOnlyList<RosterPerson>> ListRosterAsync(string token, string groupName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return Array.Empty<RosterPerson>();

        var keeper = _settings.Value.Keeper.BaseUrl.TrimEnd('/');
        var client = _httpClientFactory.CreateClient("MngKeeper");
        var people = new List<RosterPerson>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 1; page <= 20; page++)
        {
            var url = $"{keeper}/api/user?page={page}&pageSize=200&isActive=true";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Roster request failed with status {(int)response.StatusCode}.");

            var body = await response.Content.ReadFromJsonAsync<RosterPage>(JsonOptions, cancellationToken);
            foreach (var user in body?.Users ?? [])
            {
                if (string.IsNullOrWhiteSpace(user.KeycloakUserId) || !seen.Add(user.KeycloakUserId))
                    continue;
                if (user.Groups is null || !user.Groups.Any(group => string.Equals(group, groupName, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var name = $"{user.FirstName} {user.LastName}".Trim();
                people.Add(new RosterPerson
                {
                    PersonId = user.KeycloakUserId,
                    Name = string.IsNullOrWhiteSpace(name) ? user.Username ?? "Personel" : name
                });
            }

            if (body is null || page >= Math.Max(1, body.TotalPages))
                break;
        }

        return people;
    }

    public async Task<IReadOnlyList<CatalogEntry>> ListCatalogAsync(string token, string dataset, CancellationToken cancellationToken)
    {
        var rows = await ListPagesAsync(token, dataset, null, "name,color,sortOrder", cancellationToken);
        return rows.Select(row => new CatalogEntry
        {
            Id = ReadString(row, "__dataId") ?? string.Empty,
            Name = ReadString(row, "name") ?? string.Empty,
            Color = ReadString(row, "color"),
            Sort = ReadInt(row, "sortOrder")
        }).Where(entry => !string.IsNullOrWhiteSpace(entry.Id) && !string.IsNullOrWhiteSpace(entry.Name)).ToArray();
    }

    private async Task<IReadOnlyList<JsonElement>> ListPagesAsync(
        string token,
        string dataset,
        string? filter,
        string fields,
        CancellationToken cancellationToken)
    {
        const int pageSize = 500;
        const int max = 3000;
        var all = new List<JsonElement>();
        for (var skip = 0; skip < max; skip += pageSize)
        {
            var query = $"{Uri.EscapeDataString(dataset)}?limit={pageSize}&skip={skip}&expand=false&fields={Uri.EscapeDataString(fields)}";
            if (!string.IsNullOrWhiteSpace(filter))
                query += $"&filter={Uri.EscapeDataString(filter)}";
            var page = await GetRowsAsync(token, query, cancellationToken);
            all.AddRange(page);
            if (page.Count < pageSize)
                break;
        }

        return all;
    }

    private async Task<string?> ResolveLocationKeyAsync(string token, JsonElement door, CancellationToken cancellationToken)
    {
        var copied = ReadLocationKey(door);
        if (!string.IsNullOrWhiteSpace(copied))
            return copied;

        var locationId = ReadRelationId(door, "lokasyon");
        if (string.IsNullOrWhiteSpace(locationId))
            return null;

        try
        {
            var row = await GetRowAsync(token, $"pdks_lokasyonlar/{Uri.EscapeDataString(locationId)}?expand=false", cancellationToken);
            return row is null ? null : ReadString(row.Value, "locationKey");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<JsonElement?> GetRowAsync(string token, string relativeQuery, CancellationToken cancellationToken)
    {
        var dg = _settings.Value.DataGateway;
        var url = $"{dg.BaseUrl.TrimEnd('/')}/api/{dg.ApiVersion}/data/{relativeQuery}";
        var client = _httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var row = AsRow(document.RootElement);
        return row?.Clone();
    }

    private async Task<IReadOnlyList<JsonElement>> GetRowsAsync(string token, string relativeQuery, CancellationToken cancellationToken)
    {
        var dg = _settings.Value.DataGateway;
        var url = $"{dg.BaseUrl.TrimEnd('/')}/api/{dg.ApiVersion}/data/{relativeQuery}";
        var client = _httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"DataGateway read failed with status {(int)response.StatusCode}.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ExtractRows(document.RootElement);
    }

    private static IReadOnlyList<JsonElement> ExtractRows(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(item => item.Clone()).ToArray();

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
                return data.EnumerateArray().Select(item => item.Clone()).ToArray();
            if (data.ValueKind == JsonValueKind.Object)
                return new[] { data.Clone() };
        }

        return Array.Empty<JsonElement>();
    }

    private static JsonElement? AsRow(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Object)
                return data;
            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                return data[0];
        }

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            return root[0];

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("locationKey", out _))
            return root;

        return null;
    }

    private static string? ReadRelationId(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        if (value.ValueKind != JsonValueKind.Object)
            return null;
        if (value.TryGetProperty("__dataId", out var id) && id.ValueKind == JsonValueKind.String)
            return id.GetString();
        if (value.TryGetProperty("dataId", out var dataId) && dataId.ValueKind == JsonValueKind.String)
            return dataId.GetString();
        return null;
    }

    private static string? ReadLocationKey(JsonElement door)
    {
        if (!door.TryGetProperty("lokasyon", out var value) || value.ValueKind != JsonValueKind.Object)
            return null;
        return ReadString(value, "locationKey");
    }

    private async Task PatchPassageAsync(string token, string id, object body, CancellationToken cancellationToken)
    {
        var dg = _settings.Value.DataGateway;
        var url = $"{dg.BaseUrl.TrimEnd('/')}/api/{dg.ApiVersion}/data/pdks_gecisler/{Uri.EscapeDataString(id)}?skipEventPublish=true";
        var client = _httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body, options: PatchOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Passage photo update failed with status {(int)response.StatusCode}.");
    }

    private async Task DeletePhotoAsync(string token, string path, CancellationToken cancellationToken)
    {
        var dg = _settings.Value.DataGateway;
        var url = $"{dg.BaseUrl.TrimEnd('/')}/api/{dg.ApiVersion}/files?filePath={Uri.EscapeDataString(path)}";
        var client = _httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Passage photo delete failed with status {(int)response.StatusCode}.");
    }

    private static string? ReadPhotoPath(JsonElement row)
    {
        if (!row.TryGetProperty("photo", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String)
            return path.GetString();
        return null;
    }

    private static AttendanceClock ReadClock(JsonElement row, AttendanceClock fallback)
    {
        var entry = ReadString(row, "girisSaati");
        var exit = ReadString(row, "cikisSaati");
        return new AttendanceClock(
            string.IsNullOrWhiteSpace(entry) ? fallback.Entry : entry,
            string.IsNullOrWhiteSpace(exit) ? fallback.Exit : exit,
            ReadInt(row, "toleransDakika"));
    }

    private sealed class RosterPage
    {
        public List<RosterUser>? Users { get; set; }
        public int TotalPages { get; set; }
    }

    private sealed class RosterUser
    {
        public string? KeycloakUserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public List<string>? Groups { get; set; }
    }

    private static PassageRecord ToPassage(JsonElement row) => new()
    {
        Direction = ReadString(row, "direction"),
        DisplayName = ReadString(row, "displayName"),
        FullName = ReadString(row, "fullName"),
        Greeting = ReadString(row, "greeting"),
        DoorKey = ReadString(row, "doorKey"),
        OccurredAt = ReadDate(row, "occurredAt")
    };

    private static string? ReadString(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object when value.TryGetProperty("userId", out var userId) => userId.GetString(),
            _ => null
        };
    }

    private static bool ReadBool(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
            return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static int ReadInt(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return Math.Max(0, number);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var wide))
            return Math.Max(0, (int)Math.Round(wide));
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            return Math.Max(0, parsed);
        return 0;
    }

    private static DateTime? ReadDate(JsonElement row, string name)
    {
        var text = ReadString(row, name);
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}
