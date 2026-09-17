using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const int MaxSeriesOccurrences = 26;

    public async Task<MeetingSeriesDto> CreateMeetingSeriesAsync(
        string projectId,
        CreateMeetingSeriesRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = RequireMeetingName(request.Name);
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var weekday = ClampWeekday(request.Weekday);
        var startTime = NormalizeStartTime(request.StartTime);
        var duration = ClampDuration(request.DurationMinutes);
        var until = request.Until.ToUniversalTime().Date;
        var tz = MeetingTimeZone();
        var anchor = ResolveAnchorUtc(request.FirstStart, weekday, startTime, tz);

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["wbsId"] = wbsId,
            ["weekday"] = weekday,
            ["startTime"] = startTime,
            ["durationMinutes"] = duration,
            ["anchorStart"] = anchor,
            ["until"] = until,
            ["location"] = EmptyToNull(request.Location),
            ["meetingUrl"] = EmptyToNull(request.MeetingUrl),
            ["attendees"] = EmptyToNull(request.Attendees),
            ["agenda"] = EmptyToNull(request.Agenda),
            ["note"] = EmptyToNull(request.Note)
        };

        var created = await _dg.CreateAsync(PmDatasets.MeetingSeries, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Meeting series create did not return an id.", "Toplantı serisi oluşturulamadı.", 500);

        await GenerateSeriesOccurrencesAsync(id, projectId, name, wbsId, weekday, startTime, duration, anchor, until, request.Location, request.MeetingUrl, request.Attendees, request.Agenda, request.Note, token, ct);
        return await LoadSeriesDtoAsync(id, token, ct);
    }

    public async Task<MeetingSeriesDto> UpdateMeetingSeriesAsync(string id, UpdateMeetingSeriesRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadSeriesRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var name = request.Name is not null ? RequireMeetingName(request.Name) : RequireMeetingName(existing.name);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var weekday = request.Weekday.HasValue ? ClampWeekday(request.Weekday.Value) : ClampWeekday((int)(existing.weekday ?? 1));
        var startTime = request.StartTime is not null ? NormalizeStartTime(request.StartTime) : NormalizeStartTime(existing.startTime);
        var duration = request.DurationMinutes.HasValue ? ClampDuration(request.DurationMinutes.Value) : ClampDuration((int)(existing.durationMinutes ?? 60));
        var until = (request.Until ?? existing.until ?? DateTime.UtcNow).ToUniversalTime().Date;
        var tz = MeetingTimeZone();
        var anchor = existing.anchorStart ?? DateTime.UtcNow;
        if (request.Weekday.HasValue || request.StartTime is not null)
            anchor = ResolveAnchorUtc(anchor, weekday, startTime, tz);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["wbsId"] = wbsId,
            ["weekday"] = weekday,
            ["startTime"] = startTime,
            ["durationMinutes"] = duration,
            ["anchorStart"] = anchor,
            ["until"] = until
        };
        if (request.Location is not null) payload["location"] = EmptyToNull(request.Location);
        if (request.MeetingUrl is not null) payload["meetingUrl"] = EmptyToNull(request.MeetingUrl);
        if (request.Attendees is not null) payload["attendees"] = EmptyToNull(request.Attendees);
        if (request.Agenda is not null) payload["agenda"] = EmptyToNull(request.Agenda);
        if (request.Note is not null) payload["note"] = EmptyToNull(request.Note);
        await _dg.UpdateAsync(PmDatasets.MeetingSeries, id, payload, token, ct);

        var location = request.Location ?? existing.location;
        var url = request.MeetingUrl ?? existing.meetingUrl;
        var attendees = request.Attendees ?? existing.attendees;
        var agenda = request.Agenda ?? existing.agenda;
        var note = request.Note ?? existing.note;

        var meetings = await LoadMeetingRowsAsync(projectId, token, ct);
        var actions = await LoadMeetingActionRowsAsync(projectId, token, ct);
        var now = DateTime.UtcNow;
        foreach (var meeting in meetings)
        {
            if (!string.Equals(meeting.seriesId, id, StringComparison.Ordinal)) continue;
            if (string.IsNullOrWhiteSpace(meeting.__dataId)) continue;
            if (meeting.detached is >= 1) continue;
            var start = meeting.startAt ?? meeting.heldAt;
            var locked = string.Equals(PmMeetingStatus.Normalize(meeting.status), PmMeetingStatus.Held, StringComparison.Ordinal)
                || string.Equals(PmMeetingStatus.Normalize(meeting.status), PmMeetingStatus.Cancelled, StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(meeting.minutesResourceId)
                || actions.Any(a => string.Equals(a.meetingId, meeting.__dataId, StringComparison.Ordinal));
            if (locked) continue;
            if (start is not null && start.Value.ToUniversalTime() < now)
            {
                await _dg.UpdateAsync(PmDatasets.Meetings, meeting.__dataId, new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["location"] = EmptyToNull(location),
                    ["meetingUrl"] = EmptyToNull(url),
                    ["attendees"] = EmptyToNull(attendees),
                    ["agenda"] = EmptyToNull(agenda),
                    ["note"] = EmptyToNull(note),
                    ["wbsId"] = wbsId
                }, token, ct);
                continue;
            }
            await _dg.DeleteAsync(PmDatasets.Meetings, meeting.__dataId, token, ct);
        }

        await GenerateSeriesOccurrencesAsync(id, projectId, name, wbsId, weekday, startTime, duration, anchor, until, location, url, attendees, agenda, note, token, ct);
        return await LoadSeriesDtoAsync(id, token, ct);
    }

    public async Task DeleteMeetingSeriesAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadSeriesRowOrThrowAsync(id, token, ct);
        var meetings = await LoadMeetingRowsAsync(existing.projectId!, token, ct);
        var now = DateTime.UtcNow;
        foreach (var meeting in meetings)
        {
            if (!string.Equals(meeting.seriesId, id, StringComparison.Ordinal)) continue;
            if (string.IsNullOrWhiteSpace(meeting.__dataId)) continue;
            var start = meeting.startAt ?? meeting.heldAt;
            var status = PmMeetingStatus.Normalize(meeting.status);
            if (string.Equals(status, PmMeetingStatus.Held, StringComparison.Ordinal)) continue;
            if (start is not null && start.Value.ToUniversalTime() < now) continue;
            await _dg.UpdateAsync(PmDatasets.Meetings, meeting.__dataId, new Dictionary<string, object?>
            {
                ["status"] = PmMeetingStatus.Cancelled
            }, token, ct);
        }
        await _dg.UpdateAsync(PmDatasets.MeetingSeries, id, new Dictionary<string, object?>
        {
            ["until"] = DateTime.UtcNow.Date.AddDays(-1)
        }, token, ct);
    }

    private async Task GenerateSeriesOccurrencesAsync(
        string seriesId,
        string projectId,
        string name,
        string? wbsId,
        int weekday,
        string startTime,
        int duration,
        DateTime anchorUtc,
        DateTime untilDateUtc,
        string? location,
        string? meetingUrl,
        string? attendees,
        string? agenda,
        string? note,
        string token,
        CancellationToken ct)
    {
        var existing = (await LoadMeetingRowsAsync(projectId, token, ct))
            .Where(m => string.Equals(m.seriesId, seriesId, StringComparison.Ordinal))
            .ToList();
        var tz = MeetingTimeZone();
        var cursor = anchorUtc.ToUniversalTime();
        var created = 0;
        while (created < MaxSeriesOccurrences)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(cursor, DateTimeKind.Utc), tz);
            if (local.Date > untilDateUtc.Date) break;
            var occDate = DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
            var already = existing.Any(m =>
                m.occurrenceDate is not null
                && m.occurrenceDate.Value.ToUniversalTime().Date == occDate);
            if (!already)
            {
                var end = cursor.AddMinutes(duration);
                var status = InferMeetingStatus(cursor);
                await _dg.CreateAsync(PmDatasets.Meetings, new Dictionary<string, object?>
                {
                    ["projectId"] = projectId,
                    ["name"] = name,
                    ["heldAt"] = cursor,
                    ["startAt"] = cursor,
                    ["endAt"] = end,
                    ["status"] = status,
                    ["wbsId"] = wbsId,
                    ["attendees"] = EmptyToNull(attendees),
                    ["note"] = EmptyToNull(note),
                    ["location"] = EmptyToNull(location),
                    ["meetingUrl"] = EmptyToNull(meetingUrl),
                    ["agenda"] = EmptyToNull(agenda),
                    ["seriesId"] = seriesId,
                    ["occurrenceDate"] = occDate,
                    ["detached"] = 0
                }, token, ct);
            }
            created++;
            cursor = cursor.AddDays(7);
        }
    }

    private async Task<List<MeetingSeriesDto>> LoadSeriesDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var meetings = await LoadMeetingRowsAsync(projectId, token, ct);
        var actions = (await LoadMeetingActionRowsAsync(projectId, token, ct)).Select(ToMeetingActionDto).ToList();
        return await LoadSeriesDtosAsync(projectId, meetings, actions, token, ct);
    }

    private async Task<List<MeetingSeriesDto>> LoadSeriesDtosAsync(
        string projectId,
        IReadOnlyList<PmMeetingRow> meetings,
        IReadOnlyList<MeetingActionDto> actions,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadSeriesRowsAsync(projectId, token, ct);
        return rows
            .Select(row => ToSeriesDto(row, meetings, actions))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmMeetingSeriesRow>> LoadSeriesRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.MeetingSeries,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmMeetingSeriesRow>).ToList();
    }

    private async Task<PmMeetingSeriesRow> LoadSeriesRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmMeetingSeriesRow>(PmDatasets.MeetingSeries, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Meeting series not found.", "Toplantı serisi bulunamadı.", 404);
        return row;
    }

    private async Task<MeetingSeriesDto> LoadSeriesDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadSeriesRowOrThrowAsync(id, token, ct);
        var meetings = await LoadMeetingRowsAsync(row.projectId!, token, ct);
        var actions = (await LoadMeetingActionRowsAsync(row.projectId!, token, ct)).Select(ToMeetingActionDto).ToList();
        return ToSeriesDto(row, meetings, actions);
    }

    private static MeetingSeriesDto ToSeriesDto(
        PmMeetingSeriesRow row,
        IReadOnlyList<PmMeetingRow> meetings,
        IReadOnlyList<MeetingActionDto> actions)
    {
        var ids = meetings
            .Where(m => string.Equals(m.seriesId, row.__dataId, StringComparison.Ordinal) && m.detached is not >= 1)
            .Select(m => m.__dataId ?? string.Empty)
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        return new MeetingSeriesDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Name = (row.name ?? string.Empty).Trim(),
            WbsId = EmptyToNull(row.wbsId),
            Weekday = ClampWeekday((int)(row.weekday ?? 1)),
            StartTime = NormalizeStartTime(row.startTime),
            DurationMinutes = ClampDuration((int)(row.durationMinutes ?? 60)),
            AnchorStart = row.anchorStart ?? DateTime.UtcNow,
            Until = row.until ?? DateTime.UtcNow.Date,
            Location = EmptyToNull(row.location),
            MeetingUrl = EmptyToNull(row.meetingUrl),
            Attendees = EmptyToNull(row.attendees),
            Agenda = EmptyToNull(row.agenda),
            Note = EmptyToNull(row.note),
            OccurrenceCount = ids.Count,
            OpenActionCount = actions.Count(a => a.Open && ids.Contains(a.MeetingId))
        };
    }

    private static TimeZoneInfo MeetingTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
    }

    private static int ClampWeekday(int weekday) => weekday is < 1 or > 7 ? 1 : weekday;

    private static int ClampDuration(int minutes)
    {
        if (minutes < 15) return 60;
        if (minutes > 480) return 480;
        return minutes;
    }

    private static string NormalizeStartTime(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (TimeSpan.TryParse(raw, out var span))
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}";
        return "09:00";
    }

    private static DateTime ResolveAnchorUtc(DateTime first, int weekdayIso, string startTime, TimeZoneInfo tz)
    {
        var utcGuess = first.Kind == DateTimeKind.Utc ? first : DateTime.SpecifyKind(first, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcGuess, tz);
        var current = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        var add = (weekdayIso - current + 7) % 7;
        var day = local.Date.AddDays(add);
        var clock = TimeSpan.TryParse(startTime, out var span) ? span : new TimeSpan(9, 0, 0);
        var localStart = DateTime.SpecifyKind(day.Add(clock), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
    }
}
