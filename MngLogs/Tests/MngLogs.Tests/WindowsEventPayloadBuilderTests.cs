using MngLogs.Agent.EventLog;

namespace MngLogs.Tests;

public class WindowsEventPayloadBuilderTests
{
    [Fact]
    public void Build_uses_event_data_when_format_description_missing()
    {
        const string xml = """
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System><EventID>65002</EventID></System>
              <EventData>
                <Data>failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made because the target machine actively refused it.</Data>
              </EventData>
            </Event>
            """;

        var payload = WindowsEventPayloadBuilder.Build(
            65002,
            formattedMessage: null,
            properties: ["failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made because the target machine actively refused it."],
            eventXml: xml);

        Assert.Contains("rabbitmq", payload.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("192.168.20.17:5672", payload.Message);
        Assert.False(payload.Message.StartsWith("EventID ", StringComparison.Ordinal));
        Assert.Contains("rabbitmq", payload.EventDataText!, StringComparison.OrdinalIgnoreCase);
        Assert.True(payload.EventData.Count >= 1);
        Assert.False(string.IsNullOrWhiteSpace(payload.Xml));
    }

    [Fact]
    public void ParseNamedData_reads_named_and_userdata_fields()
    {
        const string xml = """
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <EventData>
                <Data Name="SubjectUserName">alice</Data>
                <Data Name="IpAddress">10.0.0.1</Data>
              </EventData>
              <UserData>
                <Custom>
                  <TargetUser>bob</TargetUser>
                </Custom>
              </UserData>
            </Event>
            """;

        var map = WindowsEventPayloadBuilder.ParseNamedData(xml);
        Assert.Equal("alice", map["SubjectUserName"]);
        Assert.Equal("10.0.0.1", map["IpAddress"]);
        Assert.Equal("bob", map["TargetUser"]);
    }

    [Fact]
    public void ApplyToFields_exposes_parser_keys()
    {
        var payload = WindowsEventPayloadBuilder.Build(
            1000,
            "rendered",
            ["a", "b"],
            """
            <Event><EventData><Data Name="msg">hello</Data></EventData></Event>
            """);

        var fields = new Dictionary<string, object?>();
        WindowsEventPayloadBuilder.ApplyToFields(fields, payload);

        Assert.Equal("rendered", payload.Message);
        Assert.True(fields.ContainsKey("eventData"));
        Assert.True(fields.ContainsKey("eventDataText"));
        Assert.True(fields.ContainsKey("properties"));
        Assert.True(fields.ContainsKey("xml"));
    }

    [Fact]
    public void Build_prefers_formatted_message_when_present()
    {
        var payload = WindowsEventPayloadBuilder.Build(
            1000,
            "Application crash",
            ["detail-only"],
            null);

        Assert.Equal("Application crash", payload.Message);
        Assert.Equal("detail-only", payload.EventDataText);
    }
}
