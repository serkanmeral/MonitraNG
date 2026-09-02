using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectCapacityDto> GetCapacityAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var wbs = (await LoadWbsAsync(projectId, token, ct)).Select(ToWbsDto).ToList();
        var assignments = await LoadAssignmentDtosAsync(projectId, wbs, token, ct);
        return BuildCapacity(assignments);
    }

    public async Task<ResourceAssignmentDto> CreateAssignmentAsync(
        string projectId,
        CreateResourceAssignmentRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var wbsId = await RequireWbsIdAsync(projectId, request.WbsId, token, ct);
        var name = RequireResourceName(request.Name);
        var hours = NormalizeHours(request.PlannedHours);
        AssertDateRange(request.Start, request.Finish);
        var personId = EmptyToNull(request.PersonId);
        await AssertAssignmentUniqueAsync(projectId, wbsId, personId, name, excludeId: null, token, ct);

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["wbsId"] = wbsId,
            ["personId"] = personId,
            ["name"] = name,
            ["role"] = EmptyToNull(request.Role),
            ["plannedHours"] = hours,
            ["start"] = request.Start,
            ["finish"] = request.Finish
        };

        var created = await _dg.CreateAsync(PmDatasets.ResourceAssignments, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Assignment create did not return an id.", "Kaynak ataması oluşturulamadı.", 500);
        return await LoadAssignmentDtoAsync(id, token, ct);
    }

    public async Task<ResourceAssignmentDto> UpdateAssignmentAsync(
        string id,
        UpdateResourceAssignmentRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadAssignmentRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var wbsId = request.WbsId is not null
            ? await RequireWbsIdAsync(projectId, request.WbsId, token, ct)
            : existing.wbsId ?? string.Empty;
        var name = request.Name is not null ? RequireResourceName(request.Name) : RequireResourceName(existing.name);
        var personId = request.PersonId is not null ? EmptyToNull(request.PersonId) : EmptyToNull(existing.personId);
        var start = request.Start ?? existing.start;
        var finish = request.Finish ?? existing.finish;
        if (request.Start.HasValue || request.Finish.HasValue)
            AssertDateRange(start, finish);
        if (request.PlannedHours.HasValue)
            NormalizeHours(request.PlannedHours.Value);

        await AssertAssignmentUniqueAsync(projectId, wbsId, personId, name, id, token, ct);

        var payload = new Dictionary<string, object?>();
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.PersonId is not null) payload["personId"] = personId;
        if (request.Name is not null) payload["name"] = name;
        if (request.Role is not null) payload["role"] = EmptyToNull(request.Role);
        if (request.PlannedHours.HasValue) payload["plannedHours"] = NormalizeHours(request.PlannedHours.Value);
        if (request.Start.HasValue) payload["start"] = request.Start;
        if (request.Finish.HasValue) payload["finish"] = request.Finish;

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.ResourceAssignments, id, payload, token, ct);
        return await LoadAssignmentDtoAsync(id, token, ct);
    }

    public async Task DeleteAssignmentAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadAssignmentRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.ResourceAssignments, id, token, ct);
    }

    private async Task<List<ResourceAssignmentDto>> LoadAssignmentDtosAsync(
        string projectId,
        IReadOnlyList<WbsItemDto> wbs,
        string token,
        CancellationToken ct)
    {
        var byId = wbs.ToDictionary(w => w.Id, StringComparer.Ordinal);
        var rows = await LoadAssignmentRowsAsync(projectId, token, ct);
        return rows
            .Select(row => ToAssignmentDto(row, byId.GetValueOrDefault(row.wbsId ?? string.Empty)))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.WbsId, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<List<PmResourceAssignmentRow>> LoadAssignmentRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.ResourceAssignments,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmResourceAssignmentRow>).ToList();
    }

    private async Task<PmResourceAssignmentRow> LoadAssignmentRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmResourceAssignmentRow>(PmDatasets.ResourceAssignments, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Assignment not found.", "Kaynak ataması bulunamadı.", 404);
        return row;
    }

    private async Task<ResourceAssignmentDto> LoadAssignmentDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadAssignmentRowOrThrowAsync(id, token, ct);
        var wbs = string.IsNullOrWhiteSpace(row.wbsId) ? null : await LoadWbsOrThrowAsync(row.wbsId, token, ct);
        return ToAssignmentDto(row, wbs is null ? null : ToWbsDto(wbs));
    }

    private async Task<string> RequireWbsIdAsync(string projectId, string? wbsId, string token, CancellationToken ct)
    {
        var id = EmptyToNull(wbsId);
        if (id is null)
            throw new OperationCoreException("WBS_REQUIRED", "WBS item is required.", "WBS kalemi zorunludur.", 400);
        var ids = await NormalizeWbsIdsAsync(projectId, new[] { id }, token, ct);
        return ids[0];
    }

    private async Task AssertAssignmentUniqueAsync(
        string projectId,
        string wbsId,
        string? personId,
        string name,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadAssignmentRowsAsync(projectId, token, ct);
        var key = ResourceKey(personId, name);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (!string.Equals(row.wbsId, wbsId, StringComparison.Ordinal))
                continue;
            if (string.Equals(ResourceKey(row.personId, row.name), key, StringComparison.Ordinal))
                throw new OperationCoreException("ASSIGNMENT_EXISTS", "This resource is already assigned to the WBS item.", "Bu kaynak bu WBS kalemine zaten atanmış.", 409);
        }
    }

    private static ResourceAssignmentDto ToAssignmentDto(PmResourceAssignmentRow row, WbsItemDto? wbs)
    {
        var start = row.start ?? wbs?.PlannedStart;
        var finish = row.finish ?? wbs?.PlannedFinish;
        NormalizeRange(ref start, ref finish);
        return new ResourceAssignmentDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            WbsId = row.wbsId ?? string.Empty,
            PersonId = EmptyToNull(row.personId),
            Name = (row.name ?? string.Empty).Trim(),
            Role = EmptyToNull(row.role),
            PlannedHours = RoundHours(row.plannedHours ?? 0),
            Start = row.start,
            Finish = row.finish,
            EffectiveStart = start,
            EffectiveFinish = finish,
            Unscheduled = start is null || finish is null
        };
    }

    internal static ProjectCapacityDto BuildCapacity(IReadOnlyList<ResourceAssignmentDto> assignments)
    {
        var groups = new Dictionary<string, PersonAcc>(StringComparer.Ordinal);
        foreach (var item in assignments)
        {
            var key = ResourceKey(item.PersonId, item.Name);
            if (!groups.TryGetValue(key, out var acc))
            {
                acc = new PersonAcc
                {
                    Key = key,
                    PersonId = item.PersonId,
                    Name = item.Name
                };
                groups[key] = acc;
            }

            acc.TotalHours += item.PlannedHours;
            if (item.Unscheduled || item.EffectiveStart is null || item.EffectiveFinish is null)
            {
                acc.UnscheduledHours += item.PlannedHours;
                continue;
            }

            SpreadHours(item.PlannedHours, item.EffectiveStart.Value, item.EffectiveFinish.Value, acc.Weeks);
        }

        var people = groups.Values
            .Select(acc =>
            {
                var weeks = acc.Weeks
                    .OrderBy(kv => kv.Key)
                    .Select(kv =>
                    {
                        var hours = RoundHours(kv.Value);
                        return new CapacityWeekDto
                        {
                            WeekStart = kv.Key,
                            Hours = hours,
                            CapacityHours = PmCapacity.WeeklyHours,
                            Overloaded = hours > PmCapacity.WeeklyHours + PmCapacity.OverloadEpsilon
                        };
                    })
                    .ToList();
                return new CapacityPersonDto
                {
                    Key = acc.Key,
                    PersonId = acc.PersonId,
                    Name = acc.Name,
                    TotalHours = RoundHours(acc.TotalHours),
                    UnscheduledHours = RoundHours(acc.UnscheduledHours),
                    WeeklyCapacityHours = PmCapacity.WeeklyHours,
                    Overloaded = weeks.Any(w => w.Overloaded)
                        || acc.UnscheduledHours > PmCapacity.WeeklyHours + PmCapacity.OverloadEpsilon,
                    Weeks = weeks
                };
            })
            .OrderByDescending(p => p.Overloaded)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProjectCapacityDto
        {
            WeeklyCapacityHours = PmCapacity.WeeklyHours,
            OverloadedCount = people.Count(p => p.Overloaded),
            Assignments = assignments,
            People = people
        };
    }

    private static void SpreadHours(double hours, DateTime start, DateTime finish, Dictionary<DateTime, double> weeks)
    {
        var days = Weekdays(start, finish);
        if (days.Count == 0)
        {
            var week = MondayUtc(start);
            weeks[week] = weeks.GetValueOrDefault(week) + hours;
            return;
        }

        var per = hours / days.Count;
        foreach (var day in days)
        {
            var week = MondayUtc(day);
            weeks[week] = weeks.GetValueOrDefault(week) + per;
        }
    }

    private static List<DateTime> Weekdays(DateTime start, DateTime finish)
    {
        var a = start.ToUniversalTime().Date;
        var b = finish.ToUniversalTime().Date;
        if (b < a) (a, b) = (b, a);
        var days = new List<DateTime>();
        for (var d = a; d <= b; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            days.Add(d);
        }
        return days;
    }

    private static DateTime MondayUtc(DateTime value)
    {
        var d = value.ToUniversalTime().Date;
        var offset = ((int)d.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(d.AddDays(-offset), DateTimeKind.Utc);
    }

    private static string ResourceKey(string? personId, string? name)
    {
        var pid = EmptyToNull(personId);
        if (pid is not null)
            return "p:" + pid;
        return "n:" + (name ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string RequireResourceName(string? name)
    {
        var n = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n))
            throw new OperationCoreException("NAME_REQUIRED", "Resource name is required.", "Kaynak adı zorunludur.", 400);
        return n;
    }

    private static double NormalizeHours(double hours)
    {
        if (hours < 0)
            throw new OperationCoreException("HOURS_RANGE", "Planned hours cannot be negative.", "Planlanan saat negatif olamaz.", 400);
        if (hours > 10_000)
            throw new OperationCoreException("HOURS_RANGE", "Planned hours is too large.", "Planlanan saat çok büyük.", 400);
        return RoundHours(hours);
    }

    private static void AssertDateRange(DateTime? start, DateTime? finish)
    {
        if (start is null || finish is null)
            return;
        if (finish.Value.ToUniversalTime().Date < start.Value.ToUniversalTime().Date)
            throw new OperationCoreException("DATE_RANGE", "Finish cannot be before start.", "Bitiş başlangıçtan önce olamaz.", 400);
    }

    private static void NormalizeRange(ref DateTime? start, ref DateTime? finish)
    {
        if (start is null || finish is null)
            return;
        if (finish.Value.ToUniversalTime().Date < start.Value.ToUniversalTime().Date)
            (start, finish) = (finish, start);
    }

    private static double RoundHours(double hours) => Math.Round(hours, 2, MidpointRounding.AwayFromZero);

    private sealed class PersonAcc
    {
        public string Key { get; set; } = string.Empty;
        public string? PersonId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double TotalHours { get; set; }
        public double UnscheduledHours { get; set; }
        public Dictionary<DateTime, double> Weeks { get; } = new();
    }
}
