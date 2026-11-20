using MngExtensions.Domain;

namespace MngExtensions.Tests;

public class PassageRulesTests
{
    private static readonly TimeZoneInfo Zone = PassageRules.IstanbulClock();

    [Fact]
    public void Hash_of_abc_is_sha256_hex()
    {
        var hash = PassageRules.HashKioskToken("abc");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        Assert.True(PassageRules.HashesMatch(hash, "abc"));
        Assert.False(PassageRules.HashesMatch(hash, "abd"));
        Assert.False(PassageRules.HashesMatch("", "abc"));
    }

    [Theory]
    [InlineData("in", "out", "in")]
    [InlineData("out", "in", "out")]
    [InlineData("auto", "in", "out")]
    [InlineData("auto", "out", "in")]
    [InlineData("auto", null, "in")]
    public void Direction_follows_mode_and_last_event(string mode, string? last, string expected)
    {
        var now = new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);
        var actual = PassageRules.ResolveDirection(mode, last, now.AddMinutes(-20), now, Zone);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Direction_resets_on_a_new_istanbul_day()
    {
        var last = new DateTime(2026, 11, 11, 20, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 11, 11, 21, 30, 0, DateTimeKind.Utc);
        var actual = PassageRules.ResolveDirection("auto", "in", last, now, Zone);
        Assert.Equal("in", actual);
    }

    [Theory]
    [InlineData(8, 59, "erken")]
    [InlineData(9, 0, "zamaninda")]
    [InlineData(9, 15, "zamaninda")]
    [InlineData(9, 16, "gec")]
    public void Entry_class_uses_clock_and_tolerance(int hour, int minute, string expected)
    {
        var local = new DateTime(2026, 11, 12, hour, minute, 0);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        Assert.Equal(expected, PassageRules.Classify(utc, "09:00", 15, Zone));
    }

    [Fact]
    public void Greeting_follows_direction_and_class()
    {
        Assert.Equal("Güle güle.", PassageRules.ChooseGreeting("out", "gec", "giriş", "zamanında", "geç", "Güle güle."));
        Assert.Equal("zamanında", PassageRules.ChooseGreeting("in", "zamaninda", "giriş", "zamanında", "geç", "veda"));
        Assert.Equal("geç", PassageRules.ChooseGreeting("in", "gec", "giriş", "zamanında", "geç", "veda"));
        Assert.Equal("giriş", PassageRules.ChooseGreeting("in", "erken", "giriş", "zamanında", "geç", "veda"));
    }

    [Theory]
    [InlineData(29, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void Duplicate_window_is_inclusive(int secondsAgo, bool duplicate)
    {
        var now = new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(duplicate, PassageRules.IsDuplicate(now.AddSeconds(-secondsAgo), now, 30));
    }

    [Fact]
    public void Break_opens_after_todays_entry_and_closes_on_return_or_exit()
    {
        var morning = new DateTime(2026, 11, 17, 6, 0, 0, DateTimeKind.Utc);
        var breakAt = morning.AddHours(3);
        var back = breakAt.AddHours(1);
        var now = back.AddMinutes(10);
        Assert.Equal("break", PassageRules.BreakChoice("in", morning, breakAt, Zone, false));
        Assert.Null(PassageRules.BreakChoice("out", morning, breakAt, Zone, false));
        Assert.Null(PassageRules.BreakChoice("in", morning.AddDays(-1), breakAt, Zone, false));

        (string?, DateTime?)[] open = [("in", morning), ("break", breakAt)];
        Assert.Equal(breakAt, PassageRules.OpenBreakStart(open, now, Zone));
        Assert.Equal("return", PassageRules.BreakChoice("in", morning, now, Zone, true));
        Assert.Null(PassageRules.OpenBreakStart([("in", morning), ("break", breakAt), ("return", back)], now, Zone));
        Assert.Null(PassageRules.OpenBreakStart([("in", morning), ("break", breakAt), ("out", back)], now, Zone));
        Assert.Equal("out", PassageRules.ChooseAction("auto", "in", morning, false, now, Zone, null));
        Assert.Equal("break", PassageRules.ChooseAction("auto", "in", morning, false, now, Zone, "break"));
        Assert.Null(PassageRules.ChooseAction("auto", "in", morning, false, now, Zone, "return"));
        Assert.Equal("return", PassageRules.ChooseAction("auto", "in", morning, true, now, Zone, "return"));
        Assert.Equal("out", PassageRules.ChooseAction("auto", "in", morning, true, now, Zone, "out"));
    }

    [Fact]
    public void Day_stays_open_through_a_break_and_closes_on_a_later_exit()
    {
        var morning = new DateTime(2026, 11, 17, 6, 0, 0, DateTimeKind.Utc);
        var breakAt = morning.AddHours(3);
        var back = breakAt.AddHours(1);
        var evening = morning.AddHours(9);
        var now = evening.AddHours(1);
        (string?, DateTime?)[] during = [("in", morning), ("break", breakAt), ("return", back)];
        Assert.False(PassageRules.DayIsComplete(during, now, Zone));
        Assert.True(PassageRules.DayIsComplete([..during, ("out", evening)], now, Zone));
    }

    [Fact]
    public void Attendance_splits_inside_break_and_exit()
    {
        var morning = new DateTime(2026, 11, 19, 6, 0, 0, DateTimeKind.Utc);
        var breakAt = morning.AddHours(3);
        var back = breakAt.AddMinutes(40);
        var evening = morning.AddHours(9);
        var now = evening.AddMinutes(-30);
        (string?, string?, string?, DateTime?)[] rows =
        [
            ("a", "Ayşe", "in", morning),
            ("a", "Ayşe", "break", breakAt),
            ("b", "Berk", "in", morning.AddMinutes(20)),
            ("c", "Cem", "in", morning.AddDays(-1))
        ];
        var open = PassageRules.Attendance(rows, breakAt.AddMinutes(10), Zone);
        var ayse = Assert.Single(open, person => person.Name == "Ayşe");
        Assert.Equal(morning, ayse.EntryUtc);
        Assert.Null(ayse.ExitUtc);
        Assert.Equal(breakAt, ayse.BreakStartUtc);
        Assert.Contains(open, person => person.Name == "Berk" && person.BreakStartUtc is null && person.ExitUtc is null);

        rows =
        [
            ("a", "Ayşe", "in", morning),
            ("a", "Ayşe", "break", breakAt),
            ("a", "Ayşe", "return", back),
            ("b", "Berk", "in", morning.AddMinutes(20)),
            ("b", "Berk", "break", breakAt),
            ("b", "Berk", "out", evening)
        ];
        var later = PassageRules.Attendance(rows, now, Zone);
        ayse = Assert.Single(later, person => person.Name == "Ayşe");
        Assert.Null(ayse.BreakStartUtc);
        Assert.Null(ayse.ExitUtc);
        var berk = Assert.Single(later, person => person.Name == "Berk");
        Assert.Equal(evening, berk.ExitUtc);
        Assert.Null(berk.BreakStartUtc);
    }

    [Fact]
    public void Live_keeps_inside_at_the_entry_location_and_moves_exit_to_its_location()
    {
        var morning = new DateTime(2026, 11, 20, 6, 0, 0, DateTimeKind.Utc);
        var breakAt = morning.AddHours(2);
        var evening = morning.AddHours(9);
        var now = evening.AddMinutes(10);
        (string?, string?, string?, DateTime?, string?)[] rows =
        [
            ("a", "Ayşe", "in", morning, "merkez"),
            ("a", "Ayşe", "break", breakAt, "merkez"),
            ("b", "Berk", "in", morning.AddMinutes(15), "merkez"),
            ("b", "Berk", "out", evening, "sube"),
            ("c", "Cem", "in", morning.AddMinutes(30), "sube")
        ];

        var people = PassageRules.Live(rows, now, Zone);
        var ayse = Assert.Single(people, person => person.Name == "Ayşe");
        var berk = Assert.Single(people, person => person.Name == "Berk");
        var cem = Assert.Single(people, person => person.Name == "Cem");

        Assert.True(PassageRules.ArrivedAt(ayse, "merkez"));
        Assert.True(PassageRules.OnBreakAt(ayse, "merkez"));
        Assert.False(PassageRules.InsideAt(ayse, "merkez"));
        Assert.True(PassageRules.ArrivedAt(berk, "merkez"));
        Assert.False(PassageRules.InsideAt(berk, "merkez"));
        Assert.True(PassageRules.LeftAt(berk, "sube"));
        Assert.False(PassageRules.LeftAt(berk, "merkez"));
        Assert.True(PassageRules.InsideAt(cem, "sube"));
        Assert.False(PassageRules.ArrivedAt(cem, "merkez"));
    }

    [Fact]
    public void Day_closes_after_entry_and_a_later_exit()
    {
        var morning = new DateTime(2026, 11, 17, 6, 0, 0, DateTimeKind.Utc);
        var evening = new DateTime(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 11, 17, 16, 0, 0, DateTimeKind.Utc);
        (string?, DateTime?)[] pair = [("in", morning), ("out", evening)];
        Assert.True(PassageRules.DayIsComplete(pair, now, Zone));
        Assert.False(PassageRules.DayIsComplete([("in", morning)], now, Zone));
        Assert.False(PassageRules.DayIsComplete([("out", evening)], now, Zone));
        Assert.False(PassageRules.DayIsComplete(pair, now.AddDays(1), Zone));
    }

    [Fact]
    public void Frame_accepts_a_jpeg_and_rejects_other_bytes()
    {
        var jpeg = new byte[40];
        jpeg[0] = 0xFF;
        jpeg[1] = 0xD8;
        jpeg[2] = 0xFF;
        var encoded = Convert.ToBase64String(jpeg);
        Assert.Equal(encoded, PassageFrame.NormalizeJpeg("data:image/jpeg;base64," + encoded));
        Assert.Null(PassageFrame.NormalizeJpeg(Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })));
        Assert.Null(PassageFrame.NormalizeJpeg(" "));
    }
}
