using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const string MeetingScanQuery = "limit=2000&expand=false";

    public async Task<ProjectMeetingsDto> GetMeetingsAsync(
        string projectId,
        MeetingListQuery? query = null,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        query ??= new MeetingListQuery();
        var meetings = await LoadMeetingRowsAsync(projectId, token, ct, MeetingScanQuery);
        var actionRows = await LoadMeetingActionRowsAsync(projectId, token, ct, MeetingScanQuery);
        var actionDtos = actionRows.Select(ToMeetingActionDto).ToList();
        var byMeeting = actionDtos
            .GroupBy(a => a.MeetingId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Open).ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);

        var all = meetings
            .Select(row => ToMeetingDto(row, byMeeting.GetValueOrDefault(row.__dataId ?? string.Empty) ?? new List<MeetingActionDto>()))
            .ToList();
        var filtered = FilterMeetings(all, query).ToList();
        var skip = Math.Max(0, query.Skip);
        var take = query.Take is null
            ? filtered.Count
            : query.Take.Value <= 0
                ? 0
                : Math.Clamp(query.Take.Value, 1, 200);
        var page = take == 0 ? Array.Empty<MeetingDto>() : filtered.Skip(skip).Take(take).ToArray();

        var pack = BuildMeetings(all);
        pack.Items = page;
        pack.Total = filtered.Count;
        pack.Skip = skip;
        pack.Take = take;
        pack.Series = query.IncludeSeries
            ? await LoadSeriesDtosAsync(projectId, meetings, actionDtos, token, ct)
            : Array.Empty<MeetingSeriesDto>();
        return pack;
    }

    public async Task<MeetingDto> CreateMeetingAsync(
        string projectId,
        CreateMeetingRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = RequireMeetingName(request.Name);
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var minutesId = NormalizeOptionalId(
            request.MinutesResourceId,
            "MINUTES_LENGTH",
            "Minutes document id is too long.",
            "Tutanak belge kimliği çok uzun.");
        var agendaId = NormalizeOptionalId(
            request.AgendaResourceId,
            "AGENDA_LENGTH",
            "Agenda document id is too long.",
            "Gündem belge kimliği çok uzun.");
        var start = request.StartAt ?? request.HeldAt;
        var end = request.EndAt ?? start?.AddMinutes(60);
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? InferMeetingStatus(start)
            : PmMeetingStatus.Normalize(request.Status);
        await AssertMeetingSlotUniqueAsync(projectId, name, start, excludeId: null, token, ct);

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["heldAt"] = start,
            ["startAt"] = start,
            ["endAt"] = end,
            ["status"] = status,
            ["minutesResourceId"] = minutesId,
            ["agendaResourceId"] = agendaId,
            ["wbsId"] = wbsId,
            ["attendees"] = EmptyToNull(request.Attendees),
            ["note"] = EmptyToNull(request.Note),
            ["location"] = EmptyToNull(request.Location),
            ["meetingUrl"] = EmptyToNull(request.MeetingUrl),
            ["agenda"] = EmptyToNull(request.Agenda),
            ["detached"] = 0
        };

        var created = await _dg.CreateAsync(PmDatasets.Meetings, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Meeting create did not return an id.", "Toplantı oluşturulamadı.", 500);
        return await LoadMeetingDtoAsync(id, token, ct);
    }

    public async Task<MeetingDto> UpdateMeetingAsync(string id, UpdateMeetingRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadMeetingRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var name = request.Name is not null ? RequireMeetingName(request.Name) : RequireMeetingName(existing.name);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var minutesId = request.MinutesResourceId is not null
            ? NormalizeOptionalId(
                request.MinutesResourceId,
                "MINUTES_LENGTH",
                "Minutes document id is too long.",
                "Tutanak belge kimliği çok uzun.")
            : EmptyToNull(existing.minutesResourceId);
        var agendaId = request.AgendaResourceId is not null
            ? NormalizeOptionalId(
                request.AgendaResourceId,
                "AGENDA_LENGTH",
                "Agenda document id is too long.",
                "Gündem belge kimliği çok uzun.")
            : EmptyToNull(existing.agendaResourceId);
        var start = request.StartAt ?? request.HeldAt ?? existing.startAt ?? existing.heldAt;
        var end = request.EndAt ?? existing.endAt ?? start?.AddMinutes(60);
        await AssertMeetingSlotUniqueAsync(projectId, name, start, id, token, ct);

        var payload = new Dictionary<string, object?>();
        if (request.Name is not null) payload["name"] = name;
        if (request.StartAt.HasValue || request.HeldAt.HasValue)
        {
            payload["startAt"] = start;
            payload["heldAt"] = start;
        }
        if (request.EndAt.HasValue || request.StartAt.HasValue || request.HeldAt.HasValue) payload["endAt"] = end;
        if (request.Status is not null) payload["status"] = PmMeetingStatus.Normalize(request.Status);
        if (request.MinutesResourceId is not null) payload["minutesResourceId"] = minutesId;
        if (request.AgendaResourceId is not null) payload["agendaResourceId"] = agendaId;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Attendees is not null) payload["attendees"] = EmptyToNull(request.Attendees);
        if (request.Note is not null) payload["note"] = EmptyToNull(request.Note);
        if (request.Location is not null) payload["location"] = EmptyToNull(request.Location);
        if (request.MeetingUrl is not null) payload["meetingUrl"] = EmptyToNull(request.MeetingUrl);
        if (request.Agenda is not null) payload["agenda"] = EmptyToNull(request.Agenda);
        if (request.Detached.HasValue)
        {
            payload["detached"] = request.Detached.Value ? 1 : 0;
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.Meetings, id, payload, token, ct);
        return await LoadMeetingDtoAsync(id, token, ct);
    }

    public async Task DeleteMeetingAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadMeetingRowOrThrowAsync(id, token, ct);
        var actions = await LoadMeetingActionRowsAsync(existing.projectId!, token, ct);
        foreach (var action in actions)
        {
            if (!string.IsNullOrWhiteSpace(action.__dataId)
                && string.Equals(action.meetingId, id, StringComparison.Ordinal))
                await _dg.DeleteAsync(PmDatasets.MeetingActions, action.__dataId, token, ct);
        }
        await _dg.DeleteAsync(PmDatasets.Meetings, id, token, ct);
    }

    public async Task<MeetingActionDto> CreateMeetingActionAsync(
        string meetingId,
        CreateMeetingActionRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var meeting = await LoadMeetingRowOrThrowAsync(meetingId, token, ct);
        var projectId = meeting.projectId!;
        var title = RequireActionTitle(request.Title);
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var workItemId = NormalizeOptionalId(
            request.WorkItemId,
            "WORKITEM_LENGTH",
            "Work item id is too long.",
            "İş kaydı kimliği çok uzun.");
        var status = PmMeetingActionStatus.Normalize(request.Status);
        var note = EmptyToNull(request.Note);
        AssertMeetingActionClose(status, note);
        await AssertMeetingActionUniqueAsync(projectId, meetingId, title, excludeId: null, token, ct);

        var closed = PmMeetingActionStatus.IsClosed(status);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["meetingId"] = meetingId,
            ["title"] = title,
            ["ownerName"] = EmptyToNull(request.OwnerName),
            ["dueDate"] = request.DueDate,
            ["status"] = status,
            ["workItemId"] = workItemId,
            ["wbsId"] = wbsId,
            ["note"] = note,
            ["closedAt"] = closed ? DateTime.UtcNow : null,
            ["closedBy"] = closed ? EmptyToNull(_ctx.Username) : null
        };

        var created = await _dg.CreateAsync(PmDatasets.MeetingActions, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Meeting action create did not return an id.", "Aksiyon oluşturulamadı.", 500);
        return await LoadMeetingActionDtoAsync(id, token, ct);
    }

    public async Task<MeetingActionDto> UpdateMeetingActionAsync(
        string id,
        UpdateMeetingActionRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadMeetingActionRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var meetingId = existing.meetingId!;
        await LoadMeetingRowOrThrowAsync(meetingId, token, ct);
        var title = request.Title is not null ? RequireActionTitle(request.Title) : RequireActionTitle(existing.title);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var workItemId = request.WorkItemId is not null
            ? NormalizeOptionalId(
                request.WorkItemId,
                "WORKITEM_LENGTH",
                "Work item id is too long.",
                "İş kaydı kimliği çok uzun.")
            : EmptyToNull(existing.workItemId);
        var status = PmMeetingActionStatus.Normalize(request.Status ?? existing.status);
        var note = request.Note is not null ? EmptyToNull(request.Note) : EmptyToNull(existing.note);
        AssertMeetingActionClose(status, note);
        await AssertMeetingActionUniqueAsync(projectId, meetingId, title, id, token, ct);

        var wasClosed = PmMeetingActionStatus.IsClosed(PmMeetingActionStatus.Normalize(existing.status));
        var nowClosed = PmMeetingActionStatus.IsClosed(status);

        var payload = new Dictionary<string, object?>();
        if (request.Title is not null) payload["title"] = title;
        if (request.OwnerName is not null) payload["ownerName"] = EmptyToNull(request.OwnerName);
        if (request.DueDate.HasValue) payload["dueDate"] = request.DueDate;
        if (request.Status is not null) payload["status"] = status;
        if (request.WorkItemId is not null) payload["workItemId"] = workItemId;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Note is not null) payload["note"] = note;

        if (!nowClosed && wasClosed)
        {
            payload["status"] = status;
            payload["closedAt"] = null;
            payload["closedBy"] = null;
        }
        else if (nowClosed && !wasClosed)
        {
            payload["status"] = status;
            payload["closedAt"] = DateTime.UtcNow;
            payload["closedBy"] = EmptyToNull(_ctx.Username);
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.MeetingActions, id, payload, token, ct);
        return await LoadMeetingActionDtoAsync(id, token, ct);
    }

    public async Task DeleteMeetingActionAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadMeetingActionRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.MeetingActions, id, token, ct);
    }

    private async Task<List<MeetingDto>> LoadMeetingDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var meetings = await LoadMeetingRowsAsync(projectId, token, ct);
        var actions = await LoadMeetingActionRowsAsync(projectId, token, ct);
        var byMeeting = actions
            .Select(ToMeetingActionDto)
            .GroupBy(a => a.MeetingId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Open).ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);

        return meetings
            .Select(row => ToMeetingDto(row, byMeeting.GetValueOrDefault(row.__dataId ?? string.Empty) ?? new List<MeetingActionDto>()))
            .OrderBy(m => m.StartAt ?? m.HeldAt)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmMeetingRow>> LoadMeetingRowsAsync(
        string projectId,
        string token,
        CancellationToken ct,
        string? query = null)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Meetings,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            query ?? ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmMeetingRow>).ToList();
    }

    private async Task<List<PmMeetingActionRow>> LoadMeetingActionRowsAsync(
        string projectId,
        string token,
        CancellationToken ct,
        string? query = null)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.MeetingActions,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            query ?? ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmMeetingActionRow>).ToList();
    }

    private async Task<PmMeetingRow> LoadMeetingRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmMeetingRow>(PmDatasets.Meetings, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Meeting not found.", "Toplantı bulunamadı.", 404);
        return row;
    }

    private async Task<PmMeetingActionRow> LoadMeetingActionRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmMeetingActionRow>(PmDatasets.MeetingActions, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Meeting action not found.", "Aksiyon bulunamadı.", 404);
        return row;
    }

    private async Task<MeetingDto> LoadMeetingDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadMeetingRowOrThrowAsync(id, token, ct);
        var actions = (await LoadMeetingActionRowsAsync(row.projectId!, token, ct))
            .Where(a => string.Equals(a.meetingId, id, StringComparison.Ordinal))
            .Select(ToMeetingActionDto)
            .OrderByDescending(a => a.Open)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ToMeetingDto(row, actions);
    }

    private async Task<MeetingActionDto> LoadMeetingActionDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadMeetingActionRowOrThrowAsync(id, token, ct);
        return ToMeetingActionDto(row);
    }

    private async Task AssertMeetingSlotUniqueAsync(
        string projectId,
        string name,
        DateTime? startAt,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        if (!startAt.HasValue) return;
        var rows = await LoadMeetingRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            var existingStart = row.startAt ?? row.heldAt;
            if (!existingStart.HasValue) continue;
            if (!string.Equals((row.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (existingStart.Value.ToUniversalTime() == startAt.Value.ToUniversalTime())
            {
                throw new OperationCoreException(
                    "MEETING_EXISTS",
                    "This meeting already exists on the project at that time.",
                    "Bu toplantı bu saatte zaten var.",
                    409);
            }
        }
    }

    private async Task AssertMeetingActionUniqueAsync(
        string projectId,
        string meetingId,
        string title,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadMeetingActionRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.Equals(row.meetingId, meetingId, StringComparison.Ordinal))
                continue;
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (string.Equals((row.title ?? string.Empty).Trim(), title, StringComparison.OrdinalIgnoreCase))
                throw new OperationCoreException(
                    "MEETING_ACTION_EXISTS",
                    "This action already exists on the meeting.",
                    "Bu aksiyon bu toplantıda zaten var.",
                    409);
        }
    }

    private static MeetingDto ToMeetingDto(PmMeetingRow row, IReadOnlyList<MeetingActionDto> actions)
    {
        var start = row.startAt ?? row.heldAt;
        var end = row.endAt ?? start?.AddMinutes(60);
        var status = string.IsNullOrWhiteSpace(row.status)
            ? InferMeetingStatus(start)
            : PmMeetingStatus.Normalize(row.status);
        return new MeetingDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Name = (row.name ?? string.Empty).Trim(),
            HeldAt = start,
            StartAt = start,
            EndAt = end,
            Status = status,
            MinutesResourceId = EmptyToNull(row.minutesResourceId),
            AgendaResourceId = EmptyToNull(row.agendaResourceId),
            WbsId = EmptyToNull(row.wbsId),
            Attendees = EmptyToNull(row.attendees),
            Note = EmptyToNull(row.note),
            Location = EmptyToNull(row.location),
            MeetingUrl = EmptyToNull(row.meetingUrl),
            Agenda = EmptyToNull(row.agenda),
            SeriesId = EmptyToNull(row.seriesId),
            OccurrenceDate = row.occurrenceDate,
            Detached = row.detached is >= 1,
            ActionCount = actions.Count,
            OpenActionCount = actions.Count(a => a.Open),
            Actions = actions
        };
    }

    private static IEnumerable<MeetingDto> FilterMeetings(IEnumerable<MeetingDto> items, MeetingListQuery query)
    {
        var kind = (query.Kind ?? "all").Trim().ToLowerInvariant();
        var seriesId = (query.SeriesId ?? string.Empty).Trim();
        var q = (query.Q ?? string.Empty).Trim();
        IEnumerable<MeetingDto> rows = items;
        if (!string.IsNullOrWhiteSpace(seriesId))
            rows = rows.Where(m => string.Equals(m.SeriesId, seriesId, StringComparison.Ordinal) && !m.Detached);
        else if (kind == "adhoc")
            rows = rows.Where(m => string.IsNullOrWhiteSpace(m.SeriesId) || m.Detached);
        else if (kind is "series" or "occurrence")
            rows = rows.Where(m => !string.IsNullOrWhiteSpace(m.SeriesId) && !m.Detached);

        if (query.From.HasValue)
        {
            var from = query.From.Value.ToUniversalTime();
            rows = rows.Where(m => (m.StartAt ?? m.HeldAt)?.ToUniversalTime() >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.ToUniversalTime();
            if (to.TimeOfDay == TimeSpan.Zero) to = to.AddDays(1);
            rows = rows.Where(m => (m.StartAt ?? m.HeldAt)?.ToUniversalTime() < to);
        }
        if (!string.IsNullOrWhiteSpace(q))
            rows = rows.Where(m => m.Name.Contains(q, StringComparison.OrdinalIgnoreCase));

        var minutes = (query.Minutes ?? "any").Trim().ToLowerInvariant();
        if (minutes is "present" or "yes" or "true")
            rows = rows.Where(m => !string.IsNullOrWhiteSpace(m.MinutesResourceId));
        else if (minutes is "missing" or "none" or "false")
        {
            var now = DateTime.UtcNow;
            rows = rows.Where(m =>
                string.IsNullOrWhiteSpace(m.MinutesResourceId)
                && !string.Equals(m.Status, PmMeetingStatus.Cancelled, StringComparison.OrdinalIgnoreCase)
                && (
                    string.Equals(m.Status, PmMeetingStatus.Held, StringComparison.OrdinalIgnoreCase)
                    || (m.StartAt ?? m.HeldAt)?.ToUniversalTime() <= now));
        }

        return rows
            .OrderByDescending(m => m.StartAt ?? m.HeldAt)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string InferMeetingStatus(DateTime? start)
    {
        if (start is null) return PmMeetingStatus.Scheduled;
        return start.Value.ToUniversalTime() < DateTime.UtcNow ? PmMeetingStatus.Held : PmMeetingStatus.Scheduled;
    }

    private static MeetingActionDto ToMeetingActionDto(PmMeetingActionRow row)
    {
        var status = PmMeetingActionStatus.Normalize(row.status);
        var open = PmMeetingActionStatus.IsOpen(status);
        var workItemId = EmptyToNull(row.workItemId);
        var due = row.dueDate?.ToUniversalTime().Date;
        return new MeetingActionDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            MeetingId = row.meetingId ?? string.Empty,
            Title = (row.title ?? string.Empty).Trim(),
            OwnerName = EmptyToNull(row.ownerName),
            DueDate = row.dueDate,
            Status = status,
            WorkItemId = workItemId,
            WbsId = EmptyToNull(row.wbsId),
            Note = EmptyToNull(row.note),
            ClosedAt = row.closedAt,
            ClosedBy = EmptyToNull(row.closedBy),
            Open = open,
            Overdue = open && due is not null && due.Value < DateTime.UtcNow.Date,
            Unbound = open && string.IsNullOrWhiteSpace(workItemId)
        };
    }

    internal static ProjectMeetingsDto BuildMeetings(IReadOnlyList<MeetingDto> items)
    {
        var actions = items.SelectMany(m => m.Actions).ToList();
        return new ProjectMeetingsDto
        {
            OpenActionCount = actions.Count(a => a.Open),
            OverdueActionCount = actions.Count(a => a.Overdue),
            UnboundActionCount = actions.Count(a => a.Unbound),
            Total = items.Count,
            Items = items
        };
    }

    internal static ProjectMeetingActionsDto BuildMeetingActions(IReadOnlyList<MeetingDto> meetings)
    {
        var items = meetings.SelectMany(m => m.Actions).ToList();
        return new ProjectMeetingActionsDto
        {
            OpenCount = items.Count(a => a.Open),
            OverdueCount = items.Count(a => a.Overdue),
            UnboundCount = items.Count(a => a.Unbound),
            Items = items
        };
    }

    private static string RequireMeetingName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "Meeting name is required.", "Toplantı adı zorunludur.", 400);
        return name;
    }

    private static string RequireActionTitle(string? value)
    {
        var title = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new OperationCoreException("TITLE_REQUIRED", "Action title is required.", "Aksiyon metni zorunludur.", 400);
        return title;
    }

    private static void AssertMeetingActionClose(string status, string? note)
    {
        if (string.Equals(status, PmMeetingActionStatus.Waived, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(note))
        {
            throw new OperationCoreException(
                "MEETING_ACTION_NOTE",
                "A note is required to waive a meeting action.",
                "Feragat için not zorunludur.",
                400);
        }
    }
}
