using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<IReadOnlyList<MeetingPersonDto>> ListMeetingPeopleAsync(string projectId, string? query, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var q = (query ?? string.Empty).Trim();
        var rows = await LoadMeetingPersonRowsAsync(projectId, token, ct);
        return rows
            .Select(ToMeetingPersonDto)
            .Where(p => q.Length == 0 || PersonMatches(p, q))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<MeetingPersonDto> CreateMeetingPersonAsync(string projectId, CreateMeetingPersonRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = RequirePersonName(request.Name);
        var email = NormalizePersonEmail(request.Email);
        var rows = await LoadMeetingPersonRowsAsync(projectId, token, ct);
        AssertPersonEmailFree(rows, email, excludeId: null);

        var created = await _dg.CreateAsync(PmDatasets.MeetingPeople, new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["organization"] = EmptyToNull(request.Organization),
            ["email"] = email,
            ["note"] = EmptyToNull(request.Note),
            ["userId"] = null
        }, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Meeting person create did not return an id.", "Kişi kartı oluşturulamadı.", 500);
        return await LoadMeetingPersonDtoAsync(id, token, ct);
    }

    public async Task<MeetingPersonDto> UpdateMeetingPersonAsync(string id, UpdateMeetingPersonRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadMeetingPersonRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId ?? string.Empty;
        var name = request.Name is not null ? RequirePersonName(request.Name) : RequirePersonName(existing.name);
        var email = request.Email is not null ? NormalizePersonEmail(request.Email) : NormalizePersonEmail(existing.email);
        var rows = await LoadMeetingPersonRowsAsync(projectId, token, ct);
        AssertPersonEmailFree(rows, email, id);
        var payload = new Dictionary<string, object?>();
        if (request.Name is not null) payload["name"] = name;
        if (request.Organization is not null) payload["organization"] = EmptyToNull(request.Organization);
        if (request.Email is not null) payload["email"] = email;
        if (request.Note is not null) payload["note"] = EmptyToNull(request.Note);
        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.MeetingPeople, id, payload, token, ct);
        return await LoadMeetingPersonDtoAsync(id, token, ct);
    }

    public async Task DeleteMeetingPersonAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadMeetingPersonRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            var attendance = await LoadAttendanceRowsAsync(projectId, token, ct);
            if (attendance.Any(row => string.Equals(row.personId, id, StringComparison.Ordinal)))
                throw new OperationCoreException(
                    "PERSON_IN_USE",
                    "This person is on a meeting.",
                    "Bu kişi bir toplantıda kayıtlı. Önce toplantıdan çıkarın.",
                    409);
        }
        await _dg.DeleteAsync(PmDatasets.MeetingPeople, id, token, ct);
    }

    public async Task<MeetingAttendanceDto> CreateMeetingAttendanceAsync(
        string meetingId,
        CreateMeetingAttendanceRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var meeting = await LoadMeetingRowOrThrowAsync(meetingId, token, ct);
        var projectId = meeting.projectId!;
        var kind = RequireAttendanceKind(request.Kind);
        var presence = ResolvePresence(request.Presence, request.Attended);
        var (expected, attended) = PmAttendancePresence.ToFlags(presence);

        string? userId = null;
        string? personId = null;
        string displayName;
        if (kind == "user")
        {
            userId = EmptyToNull(request.UserId);
            displayName = EmptyToNull(request.DisplayName) ?? string.Empty;
            if (userId is null || displayName.Length == 0)
                throw new OperationCoreException("ATTENDANCE_USER", "User attendance needs an id and a name.", "Kullanıcı seçin.", 400);
        }
        else
        {
            personId = EmptyToNull(request.PersonId);
            if (personId is null)
                throw new OperationCoreException("ATTENDANCE_PERSON", "External attendance needs a person.", "Kişi kartı seçin.", 400);
            var person = await LoadMeetingPersonRowOrThrowAsync(personId, token, ct);
            if (!string.Equals(person.projectId, projectId, StringComparison.Ordinal))
                throw new OperationCoreException(
                    "PERSON_PROJECT",
                    "This person is not defined on the project.",
                    "Bu kişi bu projenin ayarlarında yok.",
                    400);
            displayName = (person.name ?? string.Empty).Trim();
        }

        var existing = await LoadAttendanceRowsAsync(projectId, token, ct);
        if (existing.Any(r => string.Equals(r.meetingId, meetingId, StringComparison.Ordinal) && SamePerson(r, kind, userId, personId)))
            throw new OperationCoreException(
                "ATTENDANCE_EXISTS",
                "This person is already on the meeting.",
                "Bu kişi bu toplantıda zaten var.",
                409);

        var created = await _dg.CreateAsync(PmDatasets.MeetingAttendance, new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["meetingId"] = meetingId,
            ["kind"] = kind,
            ["userId"] = userId,
            ["personId"] = personId,
            ["displayName"] = displayName,
            ["presence"] = presence,
            ["expected"] = expected ? 1 : 0,
            ["attended"] = attended ? 1 : 0
        }, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Attendance create did not return an id.", "Katılım kaydı oluşturulamadı.", 500);
        return await LoadAttendanceDtoAsync(id, token, ct);
    }

    public async Task<MeetingAttendanceDto> UpdateMeetingAttendanceAsync(
        string id,
        UpdateMeetingAttendanceRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadAttendanceRowOrThrowAsync(id, token, ct);
        var presence = !string.IsNullOrWhiteSpace(request.Presence)
            ? ResolvePresence(request.Presence, null)
            : request.Attended is not null
                ? PmAttendancePresence.FromFlags(request.Attended == true)
                : ResolvePresence(existing.presence, existing.attended is >= 1);
        var (expected, attended) = PmAttendancePresence.ToFlags(presence);
        await _dg.UpdateAsync(PmDatasets.MeetingAttendance, id, new Dictionary<string, object?>
        {
            ["presence"] = presence,
            ["expected"] = expected ? 1 : 0,
            ["attended"] = attended ? 1 : 0
        }, token, ct);
        return await LoadAttendanceDtoAsync(id, token, ct);
    }

    public async Task DeleteMeetingAttendanceAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadAttendanceRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.MeetingAttendance, id, token, ct);
    }

    private async Task DeleteAttendanceForMeetingsAsync(
        string projectId,
        IReadOnlySet<string>? meetingIds,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadAttendanceRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.__dataId)) continue;
            if (meetingIds is not null && (string.IsNullOrWhiteSpace(row.meetingId) || !meetingIds.Contains(row.meetingId)))
                continue;
            await _dg.DeleteAsync(PmDatasets.MeetingAttendance, row.__dataId, token, ct);
        }
    }

    private async Task<Dictionary<string, List<MeetingAttendanceDto>>> LoadAttendanceByMeetingAsync(
        string projectId,
        string token,
        CancellationToken ct)
    {
        var people = (await LoadMeetingPersonRowsAsync(token, ct))
            .ToDictionary(r => r.__dataId ?? string.Empty, ToMeetingPersonDto, StringComparer.Ordinal);
        var rows = await LoadAttendanceRowsAsync(projectId, token, ct);
        return rows
            .Select(row => ToAttendanceDto(row, people))
            .GroupBy(a => a.MeetingId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.Ordinal);
    }

    private async Task<List<PmMeetingPersonRow>> LoadMeetingPersonRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.MeetingPeople,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            "limit=500&expand=false",
            token,
            ct);
        return page.Items.Select(Map<PmMeetingPersonRow>).ToList();
    }

    private async Task<List<PmMeetingPersonRow>> LoadMeetingPersonRowsAsync(string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.MeetingPeople,
            new Dictionary<string, object?>(),
            "limit=500&expand=false",
            token,
            ct);
        return page.Items.Select(Map<PmMeetingPersonRow>).ToList();
    }

    private async Task<List<PmMeetingAttendanceRow>> LoadAttendanceRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.MeetingAttendance,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            MeetingScanQuery,
            token,
            ct);
        return page.Items.Select(Map<PmMeetingAttendanceRow>).ToList();
    }

    private async Task<PmMeetingPersonRow> LoadMeetingPersonRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmMeetingPersonRow>(PmDatasets.MeetingPeople, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Meeting person not found.", "Kişi kartı bulunamadı.", 404);
        return row;
    }

    private async Task<MeetingPersonDto> LoadMeetingPersonDtoAsync(string id, string token, CancellationToken ct)
    {
        return ToMeetingPersonDto(await LoadMeetingPersonRowOrThrowAsync(id, token, ct));
    }

    private async Task<PmMeetingAttendanceRow> LoadAttendanceRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmMeetingAttendanceRow>(PmDatasets.MeetingAttendance, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Attendance not found.", "Katılım kaydı bulunamadı.", 404);
        return row;
    }

    private async Task<MeetingAttendanceDto> LoadAttendanceDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadAttendanceRowOrThrowAsync(id, token, ct);
        PmMeetingPersonRow? person = null;
        if (!string.IsNullOrWhiteSpace(row.personId))
            person = await _dg.GetByIdAsync<PmMeetingPersonRow>(PmDatasets.MeetingPeople, row.personId, token, ct, expand: false);
        var people = new Dictionary<string, MeetingPersonDto>(StringComparer.Ordinal);
        if (person is not null && !string.IsNullOrWhiteSpace(person.__dataId))
            people[person.__dataId] = ToMeetingPersonDto(person);
        return ToAttendanceDto(row, people);
    }

    private async Task<(string? Kind, string? UserId, string? PersonId, string? Name)> ResolveActionOwnerAsync(
        string? kindRaw,
        string? userIdRaw,
        string? personIdRaw,
        string? nameRaw,
        string token,
        CancellationToken ct)
    {
        var kind = EmptyToNull(kindRaw)?.ToLowerInvariant();
        if (kind is null)
            return (null, null, null, EmptyToNull(nameRaw));
        if (kind == "user")
        {
            var userId = EmptyToNull(userIdRaw);
            var name = EmptyToNull(nameRaw);
            if (userId is null || name is null)
                throw new OperationCoreException("OWNER_USER", "User owner needs an id and a name.", "Görev için kullanıcı seçin.", 400);
            return ("user", userId, null, name);
        }
        if (kind == "external")
        {
            var personId = EmptyToNull(personIdRaw);
            if (personId is null)
                throw new OperationCoreException("OWNER_PERSON", "External owner needs a person.", "Görev için kişi kartı seçin.", 400);
            var person = await LoadMeetingPersonRowOrThrowAsync(personId, token, ct);
            return ("external", null, personId, (person.name ?? string.Empty).Trim());
        }
        throw new OperationCoreException("OWNER_KIND", "Owner kind is invalid.", "Görev kişisinin türü geçersiz.", 400);
    }

    private static MeetingPersonDto ToMeetingPersonDto(PmMeetingPersonRow row) => new()
    {
        Id = row.__dataId ?? string.Empty,
        ProjectId = row.projectId ?? string.Empty,
        Name = (row.name ?? string.Empty).Trim(),
        Organization = EmptyToNull(row.organization),
        Email = EmptyToNull(row.email),
        Note = EmptyToNull(row.note),
        UserId = EmptyToNull(row.userId)
    };

    private static MeetingAttendanceDto ToAttendanceDto(
        PmMeetingAttendanceRow row,
        IReadOnlyDictionary<string, MeetingPersonDto> people)
    {
        people.TryGetValue(row.personId ?? string.Empty, out var person);
        var name = (row.displayName ?? string.Empty).Trim();
        if (name.Length == 0 && person is not null) name = person.Name;
        return new MeetingAttendanceDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            MeetingId = row.meetingId ?? string.Empty,
            Kind = string.Equals(row.kind, "external", StringComparison.OrdinalIgnoreCase) ? "external" : "user",
            UserId = EmptyToNull(row.userId),
            PersonId = EmptyToNull(row.personId),
            DisplayName = name,
            Organization = person?.Organization,
            Email = person?.Email,
            Presence = PmAttendancePresence.NormalizeOrNull(row.presence) ?? PmAttendancePresence.FromFlags(row.attended is >= 1),
            Expected = row.expected is >= 1,
            Attended = row.attended is >= 1
        };
    }

    private static bool PersonMatches(MeetingPersonDto person, string query)
    {
        if (person.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (person.Organization?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) return true;
        return person.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool SamePerson(PmMeetingAttendanceRow row, string kind, string? userId, string? personId)
    {
        if (kind == "user")
            return string.Equals(row.userId, userId, StringComparison.Ordinal);
        return string.Equals(row.personId, personId, StringComparison.Ordinal);
    }

    private static string RequireAttendanceKind(string? kind)
    {
        var value = (kind ?? string.Empty).Trim().ToLowerInvariant();
        if (value is "user" or "external") return value;
        throw new OperationCoreException("ATTENDANCE_KIND", "Attendance kind is invalid.", "Katılım türü geçersiz.", 400);
    }

    private static string ResolvePresence(string? presence, bool? attended)
    {
        if (!string.IsNullOrWhiteSpace(presence))
        {
            var known = PmAttendancePresence.NormalizeOrNull(presence);
            if (known is null)
                throw new OperationCoreException(
                    "ATTENDANCE_PRESENCE",
                    "Attendance presence is invalid.",
                    "Katılma durumu geçersiz.",
                    400);
            return known;
        }
        return PmAttendancePresence.FromFlags(attended == true);
    }

    private static void AssertPersonEmailFree(IEnumerable<PmMeetingPersonRow> rows, string? email, string? excludeId)
    {
        if (email is null) return;
        if (rows.Any(r =>
                !string.Equals(r.__dataId, excludeId, StringComparison.Ordinal)
                && string.Equals(NormalizePersonEmail(r.email), email, StringComparison.Ordinal)))
            throw new OperationCoreException(
                "PERSON_EMAIL_EXISTS",
                "A meeting person with this email already exists on the project.",
                "Bu e-posta bu projede kayıtlı. Onu seçin.",
                409);
    }

    private static string RequirePersonName(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new OperationCoreException("PERSON_NAME", "Person name is required.", "Kişi adı gerekli.", 400);
        if (value.Length > 256)
            throw new OperationCoreException("PERSON_NAME", "Person name is too long.", "Kişi adı çok uzun.", 400);
        return value;
    }

    private static string? NormalizePersonEmail(string? email)
    {
        var value = EmptyToNull(email);
        return value?.ToLowerInvariant();
    }
}
