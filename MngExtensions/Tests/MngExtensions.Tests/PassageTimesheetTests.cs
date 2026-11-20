using MngExtensions.Domain;

namespace MngExtensions.Tests;

public class PassageTimesheetTests
{
    private static readonly TimeZoneInfo Zone = PassageRules.IstanbulClock();
    private static readonly AttendanceClock Clocks = new("09:00", "18:00", 15);

    [Fact]
    public void Day_colors_late_entry_and_early_exit_and_keeps_the_day_on_the_entry_location()
    {
        var roster = new[] { ("a", "Ayşe"), ("b", "Berk") };
        TimesheetPass[] passes =
        [
            Pass("a", "Ayşe", "in", 20, 9, 22, "merkez", "kapi"),
            Pass("a", "Ayşe", "break", 20, 10, 0, "merkez", "kapi"),
            Pass("a", "Ayşe", "return", 20, 10, 20, "merkez", "kapi"),
            Pass("a", "Ayşe", "out", 20, 17, 40, "sube", "kapi-2")
        ];

        var people = PassageTimesheet.Build(
            roster,
            passes,
            new DateOnly(2026, 11, 19),
            new DateOnly(2026, 11, 20),
            null,
            new Dictionary<string, AttendanceClock> { ["kapi"] = Clocks, ["kapi-2"] = Clocks },
            Clocks,
            Zone);

        var ayse = Assert.Single(people, person => person.PersonId == "a");
        Assert.Equal(1, ayse.CameDays);
        Assert.Equal(1, ayse.LateEntries);
        Assert.Equal(1, ayse.EarlyExits);
        Assert.Equal(0, ayse.LateExits);
        Assert.Equal(1, ayse.BreakCount);
        Assert.Equal(20, ayse.BreakMinutes);
        Assert.Equal(478, ayse.WorkMinutes);
        Assert.False(ayse.Open);

        var missed = ayse.Days.Single(day => day.Date == new DateOnly(2026, 11, 19));
        Assert.True(missed.Absent);
        var worked = ayse.Days.Single(day => day.Date == new DateOnly(2026, 11, 20));
        Assert.Equal("09:22", worked.Entry);
        Assert.Equal("gec", worked.EntryClass);
        Assert.Equal("17:40", worked.Exit);
        Assert.Equal("erken", worked.ExitClass);
        Assert.Equal(478, worked.WorkMinutes);

        var berk = Assert.Single(people, person => person.PersonId == "b");
        Assert.Equal(0, berk.CameDays);
        Assert.Equal(0, berk.WorkMinutes);
        Assert.All(berk.Days, day => Assert.True(day.Absent));

        var atBranch = PassageTimesheet.Build(roster, passes, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 20), "sube", new Dictionary<string, AttendanceClock>(), Clocks, Zone);
        Assert.True(Assert.Single(atBranch, person => person.PersonId == "a").Days[0].Absent);
    }

    [Fact]
    public void Open_day_stays_out_of_the_total_and_a_stored_class_wins()
    {
        TimesheetPass[] passes =
        [
            Pass("a", "Ayşe", "in", 20, 8, 50, "merkez", "kapi", "zamaninda"),
            Pass("a", "Ayşe", "break", 20, 14, 0, "merkez", "kapi"),
            Pass("a", "Ayşe", "out", 20, 18, 30, "merkez", "kapi", "gec")
        ];

        var closed = PassageTimesheet.Build(
            [("a", "Ayşe")],
            passes,
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 20),
            "merkez",
            new Dictionary<string, AttendanceClock>(),
            Clocks,
            Zone);
        var ayse = Assert.Single(closed);
        var day = Assert.Single(ayse.Days);
        Assert.Equal("zamaninda", day.EntryClass);
        Assert.Equal("gec", day.ExitClass);
        Assert.Equal(1, day.BreakCount);
        Assert.Equal(270, day.BreakMinutes);
        Assert.Equal(310, ayse.WorkMinutes);

        var openPasses = passes.Take(1).ToArray();
        var open = Assert.Single(PassageTimesheet.Build([("a", "Ayşe")], openPasses, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 20), null, new Dictionary<string, AttendanceClock>(), Clocks, Zone));
        Assert.True(open.Open);
        Assert.Equal(1, open.CameDays);
        Assert.Equal(0, open.WorkMinutes);
        Assert.Null(Assert.Single(open.Days).WorkMinutes);
    }

    private static TimesheetPass Pass(
        string personId,
        string name,
        string direction,
        int day,
        int hour,
        int minute,
        string location,
        string door,
        string? sinif = null)
    {
        var local = new DateTime(2026, 11, day, hour, minute, 0);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        return new TimesheetPass(personId, name, direction, utc, location, door, sinif);
    }
}
