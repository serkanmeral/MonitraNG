using System.Text.RegularExpressions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Generic firewall syslog — kernel-style DENY key=value fields.</summary>
public sealed partial class FirewallGenericSyslogParser : ISecEventParser
{
    public const string ParserIdValue = "firewall.generic_syslog.v1";

    private static readonly Regex DenyActionRegex = DenyPattern();
    private static readonly Regex RuleChangeRegex = RuleChangePattern();
    private static readonly Regex ActorUserRegex = ActorUserPattern();
    private static readonly Regex SrcIpRegex = SrcIpPattern();
    private static readonly Regex DstIpRegex = DstIpPattern();
    private static readonly Regex DstPortRegex = DstPortPattern();
    private static readonly Regex ProtocolRegex = ProtocolPattern();

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        var type = SecEventParseHelpers.NormalizeType(raw.Source.Type);
        if (product.Equals("generic-syslog", StringComparison.OrdinalIgnoreCase)
            || type.Equals("firewall", StringComparison.OrdinalIgnoreCase))
            return true;

        var text = SecEventParseHelpers.GetRawText(raw.Raw);
        return !string.IsNullOrWhiteSpace(text)
               && (DenyActionRegex.IsMatch(text) || SrcIpRegex.IsMatch(text));
    }

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        var (action, outcome) = ClassifyAction(rawText);
        var protocol = MatchGroup(ProtocolRegex, rawText);
        var actorUser = MatchGroup(ActorUserRegex, rawText);

        return new ParsedSecEvent
        {
            Timestamp = raw.ReceivedAt,
            EventAction = action,
            EventOutcome = outcome,
            ActorUser = actorUser,
            NetworkSrcIp = MatchGroup(SrcIpRegex, rawText),
            NetworkDstIp = MatchGroup(DstIpRegex, rawText),
            NetworkDstPort = ParsePort(MatchGroup(DstPortRegex, rawText)),
            NetworkProtocol = NormalizeProtocol(protocol),
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "firewall"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, "generic-syslog"),
            SourceHost = raw.Source.Host,
            ParserId = ParserId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText)
        };
    }

    private static (string Action, string Outcome) ClassifyAction(string rawText)
    {
        if (RuleChangeRegex.IsMatch(rawText))
            return ("rule_change", "unknown");

        if (DenyActionRegex.IsMatch(rawText))
            return ("denied_flow", "failure");

        if (rawText.Contains("ALLOW", StringComparison.OrdinalIgnoreCase)
            || rawText.Contains("ACCEPT", StringComparison.OrdinalIgnoreCase))
            return ("allowed_flow", "success");

        return ("unknown", "unknown");
    }

    private static string? MatchGroup(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int? ParsePort(string? value) =>
        int.TryParse(value, out var port) ? port : null;

    private static string? NormalizeProtocol(string? protocol) =>
        string.IsNullOrWhiteSpace(protocol) ? null : protocol.ToLowerInvariant();

    [GeneratedRegex(@"\bDENY\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DenyPattern();

    [GeneratedRegex(@"\b(CONFIG\s+CHANGE|RULE_?(ADD|DEL|DELETE|UPDATE|CHANGE)|POLICY\s+CHANGE|rule\s+(added|deleted|modified|removed))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RuleChangePattern();

    [GeneratedRegex(@"\bUSER=([^\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActorUserPattern();

    [GeneratedRegex(@"SRC=([\d.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SrcIpPattern();

    [GeneratedRegex(@"DST=([\d.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex DstIpPattern();

    [GeneratedRegex(@"DPT=(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex DstPortPattern();

    [GeneratedRegex(@"PROTO=(TCP|UDP|ICMP)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProtocolPattern();
}
