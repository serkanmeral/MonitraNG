namespace MngExtensions.Domain;

public readonly record struct AttendanceClock(string Entry, string Exit, int ToleranceMinutes);

public readonly record struct TimesheetPass(
    string PersonId,
    string? Name,
    string? Direction,
    DateTime OccurredUtc,
    string? LocationKey,
    string? DoorKey,
    string? Sinif);

public sealed record TimesheetDay(
    DateOnly Date,
    bool Absent,
    bool Open,
    string Entry,
    string EntryClass,
    string Exit,
    string ExitClass,
    int BreakCount,
    int BreakMinutes,
    int? WorkMinutes);

public sealed record TimesheetPerson(
    string PersonId,
    string Name,
    int CameDays,
    int LateEntries,
    int EarlyExits,
    int LateExits,
    int BreakCount,
    int BreakMinutes,
    int WorkMinutes,
    bool Open,
    IReadOnlyList<TimesheetDay> Days);

/// <summary>
/// One row per roster person. A day belongs to the entry location.
/// Work totals include only days that have an exit. Breaks reduce the worked minutes.
/// </summary>
public static class PassageTimesheet
{
    public static IReadOnlyList<TimesheetPerson> Build(
        IEnumerable<(string PersonId, string Name)> roster,
        IEnumerable<TimesheetPass> accepted,
        DateOnly from,
        DateOnly to,
        string? locationKey,
        IReadOnlyDictionary<string, AttendanceClock> clocksByDoor,
        AttendanceClock fallback,
        TimeZoneInfo zone)
    {
        var place = string.IsNullOrWhiteSpace(locationKey) ? null : locationKey.Trim();
        var byPerson = new Dictionary<string, List<TimesheetPass>>(StringComparer.Ordinal);
        foreach (var pass in accepted)
        {
            if (string.IsNullOrWhiteSpace(pass.PersonId))
                continue;
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(pass.OccurredUtc, DateTimeKind.Utc), zone);
            var date = DateOnly.FromDateTime(local);
            if (date < from || date > to)
                continue;
            if (!byPerson.TryGetValue(pass.PersonId, out var list))
            {
                list = [];
                byPerson[pass.PersonId] = list;
            }
            list.Add(pass);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var people = new List<TimesheetPerson>();
        foreach (var person in roster)
        {
            if (string.IsNullOrWhiteSpace(person.PersonId) || !seen.Add(person.PersonId))
                continue;
            byPerson.TryGetValue(person.PersonId, out var passes);
            people.Add(Summarize(person.PersonId, person.Name, passes, from, to, place, clocksByDoor, fallback, zone));
        }

        foreach (var pair in byPerson)
        {
            if (!seen.Add(pair.Key))
                continue;
            var name = pair.Value.Select(item => item.Name).LastOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "Personel";
            people.Add(Summarize(pair.Key, name, pair.Value, from, to, place, clocksByDoor, fallback, zone));
        }

        return people;
    }

    private static TimesheetPerson Summarize(
        string personId,
        string? name,
        IReadOnlyList<TimesheetPass>? passes,
        DateOnly from,
        DateOnly to,
        string? locationKey,
        IReadOnlyDictionary<string, AttendanceClock> clocksByDoor,
        AttendanceClock fallback,
        TimeZoneInfo zone)
    {
        var days = new List<TimesheetDay>();
        for (var date = from; date <= to; date = date.AddDays(1))
            days.Add(Day(date, passes, locationKey, clocksByDoor, fallback, zone));

        return new TimesheetPerson(
            personId,
            string.IsNullOrWhiteSpace(name) ? "Personel" : name.Trim(),
            days.Count(day => !day.Absent),
            days.Count(day => !day.Absent && day.EntryClass == "gec"),
            days.Count(day => !day.Absent && !day.Open && day.ExitClass == "erken"),
            days.Count(day => !day.Absent && !day.Open && day.ExitClass == "gec"),
            days.Where(day => !day.Absent).Sum(day => day.BreakCount),
            days.Where(day => !day.Absent).Sum(day => day.BreakMinutes),
            days.Sum(day => day.WorkMinutes ?? 0),
            days.Any(day => day.Open),
            days);
    }

    private static TimesheetDay Day(
        DateOnly date,
        IReadOnlyList<TimesheetPass>? passes,
        string? locationKey,
        IReadOnlyDictionary<string, AttendanceClock> clocksByDoor,
        AttendanceClock fallback,
        TimeZoneInfo zone)
    {
        var events = (passes ?? [])
            .Select(pass => (Pass: pass, Local: TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(pass.OccurredUtc, DateTimeKind.Utc), zone)))
            .Where(item => DateOnly.FromDateTime(item.Local) == date)
            .OrderBy(item => item.Pass.OccurredUtc)
            .ToArray();

        var entry = events.FirstOrDefault(item => Is(item.Pass.Direction, "in"));
        if (entry.Pass.PersonId is null)
            return Absent(date);
        if (locationKey is not null && !string.Equals(entry.Pass.LocationKey?.Trim(), locationKey, StringComparison.Ordinal))
            return Absent(date);

        var exit = events.LastOrDefault(item =>
            Is(item.Pass.Direction, "out") && item.Pass.OccurredUtc > entry.Pass.OccurredUtc);
        var hasExit = exit.Pass.PersonId is not null;
        var (breakCount, breakMinutes) = Breaks(events, entry.Pass.OccurredUtc, hasExit ? exit.Pass.OccurredUtc : (DateTime?)null);
        var entryClock = ClockFor(entry.Pass.DoorKey, clocksByDoor, fallback);
        var entryClass = Mark(entry.Pass.Sinif, entry.Pass.OccurredUtc, entryClock.Entry, entryClock.ToleranceMinutes, zone);
        var exitClass = string.Empty;
        int? work = null;
        if (hasExit)
        {
            var exitClock = ClockFor(exit.Pass.DoorKey, clocksByDoor, fallback);
            exitClass = Mark(exit.Pass.Sinif, exit.Pass.OccurredUtc, exitClock.Exit, exitClock.ToleranceMinutes, zone);
            work = Math.Max(0, Minutes(entry.Pass.OccurredUtc, exit.Pass.OccurredUtc) - breakMinutes);
        }

        return new TimesheetDay(
            date,
            false,
            !hasExit,
            entry.Local.ToString("HH:mm"),
            entryClass,
            hasExit ? exit.Local.ToString("HH:mm") : string.Empty,
            exitClass,
            breakCount,
            breakMinutes,
            work);
    }

    private static (int Count, int Minutes) Breaks(
        IReadOnlyList<(TimesheetPass Pass, DateTime Local)> events,
        DateTime entryUtc,
        DateTime? exitUtc)
    {
        DateTime? open = null;
        var count = 0;
        var minutes = 0;
        foreach (var item in events)
        {
            if (item.Pass.OccurredUtc < entryUtc)
                continue;
            if (exitUtc is not null && item.Pass.OccurredUtc > exitUtc)
                break;
            if (Is(item.Pass.Direction, "break") && open is null)
            {
                open = item.Pass.OccurredUtc;
                continue;
            }

            if (open is null || (!Is(item.Pass.Direction, "return") && !Is(item.Pass.Direction, "out")))
                continue;
            count++;
            minutes += Minutes(open.Value, item.Pass.OccurredUtc);
            open = null;
        }

        return (count, minutes);
    }

    private static string Mark(string? sinif, DateTime occurredUtc, string expected, int tolerance, TimeZoneInfo zone)
    {
        if (Is(sinif, "erken") || Is(sinif, "zamaninda") || Is(sinif, "gec"))
            return sinif!.Trim().ToLowerInvariant();
        return PassageRules.Classify(occurredUtc, expected, tolerance, zone);
    }

    private static AttendanceClock ClockFor(
        string? doorKey,
        IReadOnlyDictionary<string, AttendanceClock> clocksByDoor,
        AttendanceClock fallback)
    {
        if (!string.IsNullOrWhiteSpace(doorKey) && clocksByDoor.TryGetValue(doorKey, out var clock))
            return clock;
        return fallback;
    }

    private static TimesheetDay Absent(DateOnly date) =>
        new(date, true, false, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, null);

    private static int Minutes(DateTime start, DateTime end) =>
        Math.Max(0, (int)Math.Floor((end - start).TotalMinutes));

    private static bool Is(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
