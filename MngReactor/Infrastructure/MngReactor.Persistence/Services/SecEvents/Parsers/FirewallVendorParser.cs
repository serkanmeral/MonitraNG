using System.Text.RegularExpressions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Vendor-specific firewall syslog — pilot: FortiGate key=value traffic/event logs (B1).</summary>
public sealed partial class FirewallVendorParser : ISecEventParser
{
    public const string ParserIdValue = "firewall.vendor.v1";
    public const string PilotProductValue = "fortigate";

    private static readonly HashSet<string> VendorProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        PilotProductValue,
        "fortinet",
        "fortigate-fw"
    };

    private static readonly Regex FortigateSniffRegex = FortigateSniffPattern();
    private static readonly Regex SrcIpRegex = SrcIpPattern();
    private static readonly Regex DstIpRegex = DstIpPattern();
    private static readonly Regex DstPortRegex = DstPortPattern();
    private static readonly Regex ProtoRegex = ProtoPattern();
    private static readonly Regex ActionRegex = ActionPattern();
    private static readonly Regex LogTypeRegex = LogTypePattern();
    private static readonly Regex UserRegex = UserPattern();
    private static readonly Regex CfgPathRegex = CfgPathPattern();

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        if (VendorProducts.Contains(product))
            return true;

        var text = SecEventParseHelpers.GetRawText(raw.Raw);
        return !string.IsNullOrWhiteSpace(text) && FortigateSniffRegex.IsMatch(text);
    }

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        var (action, outcome) = ClassifyAction(rawText);
        var actorUser = MatchGroup(UserRegex, rawText);

        return new ParsedSecEvent
        {
            Timestamp = raw.ReceivedAt,
            EventAction = action,
            EventOutcome = outcome,
            ActorUser = actorUser,
            NetworkSrcIp = MatchGroup(SrcIpRegex, rawText),
            NetworkDstIp = MatchGroup(DstIpRegex, rawText),
            NetworkDstPort = ParsePort(MatchGroup(DstPortRegex, rawText)),
            NetworkProtocol = NormalizeProtocol(MatchGroup(ProtoRegex, rawText)),
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "firewall"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, PilotProductValue),
            SourceHost = raw.Source.Host,
            ParserId = ParserId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText)
        };
    }

    private static (string Action, string Outcome) ClassifyAction(string rawText)
    {
        if (IsRuleChange(rawText))
            return ("rule_change", "unknown");

        var action = MatchGroup(ActionRegex, rawText);
        if (IsDenyAction(action))
            return ("denied_flow", "failure");

        if (IsAllowAction(action))
            return ("allowed_flow", "success");

        return ("unknown", "unknown");
    }

    private static bool IsRuleChange(string rawText)
    {
        var logType = MatchGroup(LogTypeRegex, rawText);
        if (!string.Equals(logType, "event", StringComparison.OrdinalIgnoreCase))
            return false;

        var cfgPath = MatchGroup(CfgPathRegex, rawText) ?? string.Empty;
        if (cfgPath.Contains("firewall", StringComparison.OrdinalIgnoreCase)
            || cfgPath.Contains("policy", StringComparison.OrdinalIgnoreCase))
            return true;

        var action = MatchGroup(ActionRegex, rawText);
        return action is "edit" or "add" or "delete" or "move" or "clone" or "rename";
    }

    private static bool IsDenyAction(string? action) =>
        action is "deny" or "block" or "drop" or "reject" or "blocked";

    private static bool IsAllowAction(string? action) =>
        action is "accept" or "allow" or "pass" or "permit" or "start";

    private static string? MatchGroup(Regex regex, string text)
    {
        var match = regex.Match(text);
        if (!match.Success)
            return null;

        for (var i = 1; i < match.Groups.Count; i++)
        {
            var value = match.Groups[i].Value;
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    private static int? ParsePort(string? value) =>
        int.TryParse(value, out var port) ? port : null;

    private static string? NormalizeProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
            return null;

        return protocol switch
        {
            "6" => "tcp",
            "17" => "udp",
            "1" => "icmp",
            _ => protocol.ToLowerInvariant()
        };
    }

    [GeneratedRegex(@"\bdevname=\S+.*\btype=""(traffic|event)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateSniffPattern();

    [GeneratedRegex(@"\bsrcip=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SrcIpPattern();

    [GeneratedRegex(@"\bdstip=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DstIpPattern();

    [GeneratedRegex(@"\bdstport=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DstPortPattern();

    [GeneratedRegex(@"\bproto=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProtoPattern();

    [GeneratedRegex(@"\baction=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActionPattern();

    [GeneratedRegex(@"\btype=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LogTypePattern();

    [GeneratedRegex(@"\buser=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserPattern();

    [GeneratedRegex(@"\bcfgpath=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CfgPathPattern();
}
