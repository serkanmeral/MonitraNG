using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectMeetingsDto> GetMeetingsAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var items = await LoadMeetingDtosAsync(projectId, token, ct);
        return BuildMeetings(items);
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
        await AssertMeetingUniqueAsync(projectId, name, excludeId: null, token, ct);

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["heldAt"] = request.HeldAt,
            ["minutesResourceId"] = minutesId,
            ["wbsId"] = wbsId,
            ["attendees"] = EmptyToNull(request.Attendees),
            ["note"] = EmptyToNull(request.Note)
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
        await AssertMeetingUniqueAsync(projectId, name, id, token, ct);

        var payload = new Dictionary<string, object?>();
        if (request.Name is not null) payload["name"] = name;
        if (request.HeldAt.HasValue) payload["heldAt"] = request.HeldAt;
        if (request.MinutesResourceId is not null) payload["minutesResourceId"] = minutesId;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Attendees is not null) payload["attendees"] = EmptyToNull(request.Attendees);
        if (request.Note is not null) payload["note"] = EmptyToNull(request.Note);

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
            .OrderByDescending(m => m.OpenActionCount)
            .ThenByDescending(m => m.HeldAt)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmMeetingRow>> LoadMeetingRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Meetings,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmMeetingRow>).ToList();
    }

    private async Task<List<PmMeetingActionRow>> LoadMeetingActionRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.MeetingActions,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
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

    private async Task AssertMeetingUniqueAsync(
        string projectId,
        string name,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadMeetingRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (string.Equals((row.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                throw new OperationCoreException(
                    "MEETING_EXISTS",
                    "This meeting already exists on the project.",
                    "Bu toplantı bu projede zaten var.",
                    409);
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
        return new MeetingDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Name = (row.name ?? string.Empty).Trim(),
            HeldAt = row.heldAt,
            MinutesResourceId = EmptyToNull(row.minutesResourceId),
            WbsId = EmptyToNull(row.wbsId),
            Attendees = EmptyToNull(row.attendees),
            Note = EmptyToNull(row.note),
            ActionCount = actions.Count,
            OpenActionCount = actions.Count(a => a.Open),
            Actions = actions
        };
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
