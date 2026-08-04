using MngReactor.Application.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventFieldQueryHelperTests
{
    [Fact]
    public void ParseFieldFiltersJson_ParsesValidClauses()
    {
        var raw = """[{"field":"custom.session_id","op":"eq","value":"s1"},{"field":"message","op":"contains","value":"x"}]""";
        var list = SecEventFieldQueryHelper.ParseFieldFiltersJson(raw);
        Assert.Equal(2, list.Count);
        Assert.Equal("custom.session_id", list[0].Field);
        Assert.Equal("eq", list[0].Op);
        Assert.Equal("s1", list[0].Value);
        Assert.Equal("message", list[1].Field);
        Assert.Equal("contains", list[1].Op);
    }

    [Fact]
    public void ParseFieldFiltersJson_SkipsDisallowedFields()
    {
        var raw = """[{"field":"raw","op":"eq","value":"x"},{"field":"actor.user","op":"eq","value":"a"}]""";
        var list = SecEventFieldQueryHelper.ParseFieldFiltersJson(raw);
        Assert.Single(list);
        Assert.Equal("actor.user", list[0].Field);
    }

    [Fact]
    public void ParseFieldFiltersJson_InvalidJson_ReturnsEmpty()
    {
        Assert.Empty(SecEventFieldQueryHelper.ParseFieldFiltersJson("{not-json"));
        Assert.Empty(SecEventFieldQueryHelper.ParseFieldFiltersJson(null));
    }

    [Fact]
    public void IsBagField_CustomAndExtras()
    {
        Assert.True(SecEventFieldQueryHelper.IsBagField("custom.foo"));
        Assert.True(SecEventFieldQueryHelper.IsBagField("message"));
        Assert.False(SecEventFieldQueryHelper.IsBagField("actor.user"));
    }
}
