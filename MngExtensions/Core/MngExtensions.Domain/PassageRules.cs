using System.Security.Cryptography;
using System.Text;

namespace MngExtensions.Domain;

/// <summary>
/// Door-passage rules that do not depend on a database or a clock service.
/// </summary>
public static class PassageRules
{
    public static TimeZoneInfo IstanbulClock()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Istanbul", out var zone))
            return zone;
        return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
    }

    public static string HashKioskToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool HashesMatch(string storedHash, string presentedToken)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrEmpty(presentedToken))
            return false;

        var presented = HashKioskToken(presentedToken);
        var left = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());
        var right = Encoding.UTF8.GetBytes(presented);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    /// <summary>
    /// auto: last accepted event today in Istanbul. in stays in, out stays out.
    /// A previous day, or no event, opens a new entry.
    /// </summary>
    public static string ResolveDirection(
        string? mode,
        string? lastDirection,
        DateTime? lastOccurredUtc,
        DateTime nowUtc,
        TimeZoneInfo zone)
    {
        if (string.Equals(mode, "in", StringComparison.OrdinalIgnoreCase))
            return "in";
        if (string.Equals(mode, "out", StringComparison.OrdinalIgnoreCase))
            return "out";

        if (lastOccurredUtc is null || string.IsNullOrWhiteSpace(lastDirection))
            return "in";

        var lastLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(lastOccurredUtc.Value, DateTimeKind.Utc), zone);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        if (lastLocal.Date != nowLocal.Date)
            return "in";

        return string.Equals(lastDirection, "in", StringComparison.OrdinalIgnoreCase) ? "out" : "in";
    }

    public static bool IsWork(string? direction) =>
        IsDirection(direction, "in") || IsDirection(direction, "out");

    /// <summary>
    /// An open break is a break today with no later return or exit on the same Istanbul date.
    /// An exit closes the break. A break from a previous day stays unfinished.
    /// </summary>
    public static DateTime? OpenBreakStart(
        IEnumerable<(string? Direction, DateTime? OccurredUtc)> accepted,
        DateTime nowUtc,
        TimeZoneInfo zone)
    {
        var today = LocalDate(nowUtc, zone);
        DateTime? open = null;
        foreach (var item in accepted
            .Where(item => item.OccurredUtc is not null && LocalDate(item.OccurredUtc.Value, zone) == today)
            .OrderBy(item => item.OccurredUtc))
        {
            var at = DateTime.SpecifyKind(item.OccurredUtc!.Value, DateTimeKind.Utc);
            if (IsDirection(item.Direction, "break"))
                open = at;
            else if (IsDirection(item.Direction, "return") || IsDirection(item.Direction, "out"))
                open = null;
        }

        return open;
    }

    /// <summary>
    /// Return while a break is open. Break only after today's accepted entry. Otherwise no break button.
    /// </summary>
    public static string? BreakChoice(
        string? lastWorkDirection,
        DateTime? lastWorkUtc,
        DateTime nowUtc,
        TimeZoneInfo zone,
        bool breakOpen)
    {
        if (breakOpen)
            return "return";
        if (lastWorkUtc is null || !IsDirection(lastWorkDirection, "in"))
            return null;

        var lastLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(lastWorkUtc.Value, DateTimeKind.Utc), zone);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        return lastLocal.Date == nowLocal.Date ? "break" : null;
    }

    /// <summary>
    /// Empty request keeps the door direction. A named request must be the one currently offered.
    /// </summary>
    public static string? ChooseAction(
        string? mode,
        string? lastWorkDirection,
        DateTime? lastWorkUtc,
        bool breakOpen,
        DateTime nowUtc,
        TimeZoneInfo zone,
        string? requested)
    {
        var passage = ResolveDirection(mode, lastWorkDirection, lastWorkUtc, nowUtc, zone);
        var breakChoice = BreakChoice(lastWorkDirection, lastWorkUtc, nowUtc, zone, breakOpen);
        var asked = string.IsNullOrWhiteSpace(requested) ? passage : requested.Trim();
        if (IsWork(asked))
            return string.Equals(asked, passage, StringComparison.OrdinalIgnoreCase) ? passage : null;
        if (IsDirection(asked, "break") || IsDirection(asked, "return"))
            return string.Equals(asked, breakChoice, StringComparison.OrdinalIgnoreCase) ? asked.ToLowerInvariant() : null;
        return null;
    }

    public readonly record struct AttendancePerson(string Name, DateTime? EntryUtc, DateTime? ExitUtc, DateTime? BreakStartUtc);

    /// <summary>
    /// One row per person for the Istanbul day. An open break is not an exit.
    /// A break from another day is ignored.
    /// </summary>
    public static IReadOnlyList<AttendancePerson> Attendance(
        IEnumerable<(string? PersonId, string? Name, string? Direction, DateTime? OccurredUtc)> accepted,
        DateTime nowUtc,
        TimeZoneInfo zone)
    {
        var today = LocalDate(nowUtc, zone);
        var groups = new Dictionary<string, List<(string Name, string? Direction, DateTime At)>>(StringComparer.Ordinal);
        foreach (var item in accepted)
        {
            if (string.IsNullOrWhiteSpace(item.PersonId) || item.OccurredUtc is null)
                continue;
            var at = DateTime.SpecifyKind(item.OccurredUtc.Value, DateTimeKind.Utc);
            if (LocalDate(at, zone) != today)
                continue;
            if (!groups.TryGetValue(item.PersonId, out var list))
            {
                list = [];
                groups[item.PersonId] = list;
            }
            list.Add((item.Name?.Trim() ?? string.Empty, item.Direction, at));
        }

        var people = new List<AttendancePerson>();
        foreach (var pair in groups)
        {
            var events = pair.Value.OrderBy(item => item.At).ToArray();
            DateTime? entry = null;
            DateTime? exit = null;
            var name = string.Empty;
            foreach (var item in events)
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                    name = item.Name;
                if (entry is null && IsDirection(item.Direction, "in"))
                    entry = item.At;
                if (IsDirection(item.Direction, "out") && (entry is null || item.At > entry))
                    exit = item.At;
            }

            var breakStart = OpenBreakStart(events.Select(item => ((string?)item.Direction, (DateTime?)item.At)), nowUtc, zone);
            if (entry is null && exit is null && breakStart is null)
                continue;
            people.Add(new AttendancePerson(name, entry, exit, breakStart));
        }

        return people;
    }

    public readonly record struct LivePerson(
        string Name,
        DateTime? EntryUtc,
        string? EntryLocation,
        DateTime? ExitUtc,
        string? ExitLocation,
        DateTime? BreakStartUtc);

    /// <summary>
    /// Company-wide day state, then a location. Inside and break stay at the entry location.
    /// An exit places the person at the exit location, including when that location differs.
    /// </summary>
    public static IReadOnlyList<LivePerson> Live(
        IEnumerable<(string? PersonId, string? Name, string? Direction, DateTime? OccurredUtc, string? LocationKey)> accepted,
        DateTime nowUtc,
        TimeZoneInfo zone)
    {
        var today = LocalDate(nowUtc, zone);
        var groups = new Dictionary<string, List<(string Name, string? Direction, DateTime At, string? Location)>>(StringComparer.Ordinal);
        foreach (var item in accepted)
        {
            if (string.IsNullOrWhiteSpace(item.PersonId) || item.OccurredUtc is null)
                continue;
            var at = DateTime.SpecifyKind(item.OccurredUtc.Value, DateTimeKind.Utc);
            if (LocalDate(at, zone) != today)
                continue;
            if (!groups.TryGetValue(item.PersonId, out var list))
            {
                list = [];
                groups[item.PersonId] = list;
            }
            list.Add((item.Name?.Trim() ?? string.Empty, item.Direction, at, item.LocationKey?.Trim()));
        }

        var people = new List<LivePerson>();
        foreach (var pair in groups)
        {
            var events = pair.Value.OrderBy(item => item.At).ToArray();
            DateTime? entry = null;
            string? entryLocation = null;
            DateTime? exit = null;
            string? exitLocation = null;
            var name = string.Empty;
            foreach (var item in events)
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                    name = item.Name;
                if (entry is null && IsDirection(item.Direction, "in"))
                {
                    entry = item.At;
                    entryLocation = item.Location;
                }
                if (IsDirection(item.Direction, "out") && (entry is null || item.At > entry))
                {
                    exit = item.At;
                    exitLocation = string.IsNullOrWhiteSpace(item.Location) ? entryLocation : item.Location;
                }
            }

            var breakStart = OpenBreakStart(events.Select(item => ((string?)item.Direction, (DateTime?)item.At)), nowUtc, zone);
            if (entry is null && exit is null && breakStart is null)
                continue;
            people.Add(new LivePerson(name, entry, entryLocation, exit, exitLocation, breakStart));
        }

        return people;
    }

    public static bool ArrivedAt(LivePerson person, string locationKey) =>
        SameLocation(person.EntryLocation, locationKey);

    public static bool InsideAt(LivePerson person, string locationKey) =>
        person.EntryUtc is not null && person.ExitUtc is null && person.BreakStartUtc is null && ArrivedAt(person, locationKey);

    public static bool OnBreakAt(LivePerson person, string locationKey) =>
        person.EntryUtc is not null && person.ExitUtc is null && person.BreakStartUtc is not null && ArrivedAt(person, locationKey);

    public static bool LeftAt(LivePerson person, string locationKey) =>
        person.ExitUtc is not null && SameLocation(person.ExitLocation, locationKey);

    private static bool SameLocation(string? left, string locationKey) =>
        !string.IsNullOrWhiteSpace(locationKey)
        && string.Equals(left?.Trim(), locationKey.Trim(), StringComparison.Ordinal);

    public const string BreakGreeting = "Mola başladı.";
    public const string ReturnGreeting = "Hoş geldiniz.";
    public const string BreakClosedByExit = "Mola çıkışla kapandı.";

    /// <summary>
    /// A day closes after an accepted entry and a later accepted exit on the same Istanbul date.
    /// A door locked to entry or exit does not open that day again.
    /// </summary>
    public static bool DayIsComplete(
        IEnumerable<(string? Direction, DateTime? OccurredUtc)> accepted,
        DateTime nowUtc,
        TimeZoneInfo zone)
    {
        var today = LocalDate(nowUtc, zone);
        DateTime? firstEntry = null;
        foreach (var item in accepted)
        {
            if (item.OccurredUtc is null || !IsDirection(item.Direction, "in") || LocalDate(item.OccurredUtc.Value, zone) != today)
                continue;
            var utc = DateTime.SpecifyKind(item.OccurredUtc.Value, DateTimeKind.Utc);
            if (firstEntry is null || utc < firstEntry)
                firstEntry = utc;
        }

        if (firstEntry is null)
            return false;

        foreach (var item in accepted)
        {
            if (item.OccurredUtc is null || !IsDirection(item.Direction, "out") || LocalDate(item.OccurredUtc.Value, zone) != today)
                continue;
            if (DateTime.SpecifyKind(item.OccurredUtc.Value, DateTimeKind.Utc) > firstEntry)
                return true;
        }

        return false;
    }

    private static bool IsDirection(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static DateTime LocalDate(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).Date;

    public static string Classify(DateTime occurredUtc, string? expectedClock, int toleranceMinutes, TimeZoneInfo zone)
    {
        if (!TryParseClock(expectedClock, out var expected))
            return string.Empty;

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(occurredUtc, DateTimeKind.Utc), zone);
        var clock = local.TimeOfDay;
        var until = expected.Add(TimeSpan.FromMinutes(Math.Max(0, toleranceMinutes)));
        if (clock < expected)
            return "erken";
        if (clock <= until)
            return "zamaninda";
        return "gec";
    }

    public static string ChooseGreeting(string? direction, string? sinif, string? entry, string? onTime, string? late, string? exit)
    {
        if (string.Equals(direction, "out", StringComparison.OrdinalIgnoreCase))
            return First(exit, "Güle güle.");
        if (string.Equals(sinif, "zamaninda", StringComparison.OrdinalIgnoreCase))
            return First(onTime, "Zamanında geldiğiniz için teşekkürler.");
        if (string.Equals(sinif, "gec", StringComparison.OrdinalIgnoreCase))
            return First(late, "Yolunuz düştü, yine de hoş geldiniz.");
        return First(entry, "İyi çalışmalar.");
    }

    public static bool IsDuplicate(DateTime? lastAcceptedUtc, DateTime nowUtc, int windowSeconds)
    {
        if (lastAcceptedUtc is null || windowSeconds < 0)
            return false;

        var delta = nowUtc - DateTime.SpecifyKind(lastAcceptedUtc.Value, DateTimeKind.Utc);
        return delta >= TimeSpan.Zero && delta.TotalSeconds <= windowSeconds;
    }

    private static string First(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static bool TryParseClock(string? value, out TimeSpan clock)
    {
        clock = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var parts = value.Trim().Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var hour)
            || !int.TryParse(parts[1], out var minute)
            || hour is < 0 or > 23
            || minute is < 0 or > 59)
            return false;
        clock = new TimeSpan(hour, minute, 0);
        return true;
    }
}
