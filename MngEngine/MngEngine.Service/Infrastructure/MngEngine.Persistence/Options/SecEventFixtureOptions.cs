namespace MngEngine.Persistence.Options;

public sealed class SecEventFixtureOptions
{
    public const string SectionName = "MngEngine:SecEventFixtures";

    /// <summary>Fixture kök dizini (firewall_deny.syslog.txt vb.).</summary>
    public string Path { get; set; } = "fixtures/siem";
}
