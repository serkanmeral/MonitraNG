using MngLogCollector.Persistence.OpenSearch;

namespace MngLogCollector.Tests;

public class OpenSearchIndexNamesTests
{
    [Fact]
    public void BuildDailySecEventsIndexName_sanitizes_domain_and_date()
    {
        var name = OpenSearchIndexNames.BuildDailySecEventsIndexName(
            "Odak.Local",
            new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("mng-odak-local-sec-events-2026.07.29", name);
    }

    [Fact]
    public void SanitizeDomain_falls_back_to_unknown()
    {
        Assert.Equal("unknown", OpenSearchIndexNames.SanitizeDomain("   "));
    }
}
