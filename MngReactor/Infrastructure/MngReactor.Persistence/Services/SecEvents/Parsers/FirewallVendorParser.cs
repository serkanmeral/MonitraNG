using System.Text.RegularExpressions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Vendor-specific firewall syslog — FortiGate + Palo Alto PAN-OS + Cisco ASA (B1).</summary>
public sealed partial class FirewallVendorParser : ISecEventParser
{
    public const string ParserIdValue = "firewall.vendor.v1";
    public const string FortigateProductValue = "fortigate";
    public const string PanOsProductValue = "pan-os";
    public const string CiscoAsaProductValue = "cisco-asa";

    private static readonly HashSet<string> FortigateProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        FortigateProductValue,
        "fortinet",
        "fortigate-fw"
    };

    private static readonly HashSet<string> PanOsProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        PanOsProductValue,
        "panos",
        "paloalto",
        "palo-alto",
        "palo_alto"
    };

    private static readonly HashSet<string> CiscoAsaProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        CiscoAsaProductValue,
        "cisco-asa",
        "asa",
        "cisco-ftd",
        "ftd"
    };

    private static readonly Regex FortigateSniffRegex = FortigateSniffPattern();
    private static readonly Regex PanOsCefSniffRegex = PanOsCefSniffPattern();
    private static readonly Regex PanOsCsvSniffRegex = PanOsCsvSniffPattern();
    private static readonly Regex AsaSniffRegex = AsaSniffPattern();
    private static readonly Regex AsaDenyRegex = AsaDenyPattern();
    private static readonly Regex AsaBuiltRegex = AsaBuiltPattern();
    private static readonly Regex AsaConfigUserRegex = AsaConfigUserPattern();

    private static readonly Regex FortigateSrcIpRegex = FortigateSrcIpPattern();
    private static readonly Regex FortigateDstIpRegex = FortigateDstIpPattern();
    private static readonly Regex FortigateDstPortRegex = FortigateDstPortPattern();
    private static readonly Regex FortigateSrcPortRegex = FortigateSrcPortPattern();
    private static readonly Regex FortigateProtoRegex = FortigateProtoPattern();
    private static readonly Regex FortigateActionRegex = FortigateActionPattern();
    private static readonly Regex FortigateLogTypeRegex = FortigateLogTypePattern();
    private static readonly Regex FortigateLogSubtypeRegex = FortigateLogSubtypePattern();
    private static readonly Regex FortigateUserRegex = FortigateUserPattern();
    private static readonly Regex FortigateCfgPathRegex = FortigateCfgPathPattern();
    private static readonly Regex FortigatePolicyIdRegex = FortigatePolicyIdPattern();
    private static readonly Regex FortigateServiceRegex = FortigateServicePattern();
    private static readonly Regex FortigateDevNameRegex = FortigateDevNamePattern();
    private static readonly Regex FortigateLogIdRegex = FortigateLogIdPattern();

    private static readonly Regex CefExtensionRegex = CefExtensionPattern();
    private static readonly Regex PanOsTrafficHeaderRegex = PanOsTrafficHeaderPattern();
    private static readonly Regex PanOsConfigHeaderRegex = PanOsConfigHeaderPattern();

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        if (FortigateProducts.Contains(product) || PanOsProducts.Contains(product) || CiscoAsaProducts.Contains(product))
            return true;

        var text = SecEventParseHelpers.GetRawText(raw.Raw);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return FortigateSniffRegex.IsMatch(text)
            || PanOsCefSniffRegex.IsMatch(text)
            || PanOsCsvSniffRegex.IsMatch(text)
            || AsaSniffRegex.IsMatch(text);
    }

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);

        if (PanOsProducts.Contains(product) || IsPanOsFormat(rawText))
            return ParsePanOs(raw, rawText, product);

        if (CiscoAsaProducts.Contains(product) || IsAsaFormat(rawText))
            return ParseAsa(raw, rawText, product);

        return ParseFortigate(raw, rawText);
    }

    private static ParsedSecEvent ParseFortigate(SecEventRawContext raw, string rawText)
    {
        var (action, outcome) = ClassifyFortigateAction(rawText);
        var actorUser = MatchGroup(FortigateUserRegex, rawText);
        var devName = MatchGroup(FortigateDevNameRegex, rawText);

        return BuildParsed(raw, rawText, action, outcome, actorUser, FortigateProductValue,
            MatchGroup(FortigateSrcIpRegex, rawText),
            MatchGroup(FortigateDstIpRegex, rawText),
            ParsePort(MatchGroup(FortigateDstPortRegex, rawText)),
            NormalizeProtocol(MatchGroup(FortigateProtoRegex, rawText)),
            eventCode: MatchGroup(FortigateLogIdRegex, rawText),
            sourceHostOverride: devName,
            extraFields: BuildFortigateExtraFields(rawText));
    }

    private static IReadOnlyDictionary<string, object?> BuildFortigateExtraFields(string rawText)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        AddExtra(fields, "custom.policy_id", MatchGroup(FortigatePolicyIdRegex, rawText));
        AddExtra(fields, "custom.service", MatchGroup(FortigateServiceRegex, rawText));
        AddExtra(fields, "custom.log_type", MatchGroup(FortigateLogTypeRegex, rawText));
        AddExtra(fields, "custom.log_subtype", MatchGroup(FortigateLogSubtypeRegex, rawText));
        AddExtra(fields, "custom.cfg_path", MatchGroup(FortigateCfgPathRegex, rawText));

        var srcPort = ParsePort(MatchGroup(FortigateSrcPortRegex, rawText));
        if (srcPort.HasValue)
            fields["custom.src_port"] = srcPort.Value;

        return fields;
    }

    private static void AddExtra(IDictionary<string, object?> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            fields[key] = value;
    }

    private static ParsedSecEvent ParseAsa(SecEventRawContext raw, string rawText, string productHint)
    {
        var (action, outcome, actorUser, srcIp, dstIp, dstPort, protocol) = ClassifyAsa(rawText);

        return BuildParsed(raw, rawText, action, outcome, actorUser,
            ResolveAsaProduct(productHint),
            srcIp, dstIp, dstPort, protocol);
    }

    private static bool IsAsaFormat(string rawText) => AsaSniffRegex.IsMatch(rawText);

    private static string ResolveAsaProduct(string productHint) =>
        CiscoAsaProducts.Contains(productHint) ? productHint : CiscoAsaProductValue;

    private static (string Action, string Outcome, string? ActorUser, string? SrcIp, string? DstIp, int? DstPort, string? Protocol)
        ClassifyAsa(string rawText)
    {
        if (AsaConfigUserRegex.IsMatch(rawText))
        {
            var user = MatchGroup(AsaConfigUserRegex, rawText);
            return ("rule_change", "unknown", user, null, null, null, null);
        }

        var deny = AsaDenyRegex.Match(rawText);
        if (deny.Success)
        {
            return (
                "denied_flow",
                "failure",
                null,
                deny.Groups["srcIp"].Value,
                deny.Groups["dstIp"].Value,
                ParsePort(deny.Groups["dstPort"].Value),
                NormalizeProtocol(deny.Groups["proto"].Value));
        }

        var built = AsaBuiltRegex.Match(rawText);
        if (built.Success)
        {
            return (
                "allowed_flow",
                "success",
                null,
                built.Groups["srcIp"].Value,
                built.Groups["dstIp"].Value,
                ParsePort(built.Groups["dstPort"].Value),
                NormalizeProtocol(built.Groups["proto"].Value));
        }

        return ("unknown", "unknown", null, null, null, null, null);
    }

    private static ParsedSecEvent ParsePanOs(SecEventRawContext raw, string rawText, string productHint)
    {
        var (action, outcome, actorUser, srcIp, dstIp, dstPort, protocol) = ClassifyPanOs(rawText);

        return BuildParsed(raw, rawText, action, outcome, actorUser,
            ResolvePanOsProduct(productHint),
            srcIp, dstIp, dstPort, protocol);
    }

    private static ParsedSecEvent BuildParsed(
        SecEventRawContext raw,
        string rawText,
        string action,
        string outcome,
        string? actorUser,
        string sourceProduct,
        string? srcIp,
        string? dstIp,
        int? dstPort,
        string? protocol,
        string? eventCode = null,
        string? sourceHostOverride = null,
        IReadOnlyDictionary<string, object?>? extraFields = null) =>
        new()
        {
            Timestamp = raw.ReceivedAt,
            EventAction = action,
            EventOutcome = outcome,
            EventCode = eventCode,
            ActorUser = actorUser,
            NetworkSrcIp = srcIp,
            NetworkDstIp = dstIp,
            NetworkDstPort = dstPort,
            NetworkProtocol = protocol,
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "firewall"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, sourceProduct),
            SourceHost = !string.IsNullOrWhiteSpace(sourceHostOverride)
                ? sourceHostOverride
                : raw.Source.Host,
            ParserId = ParserIdValue,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText),
            ExtraFields = extraFields ?? new Dictionary<string, object?>(StringComparer.Ordinal)
        };

    private static bool IsPanOsFormat(string rawText) =>
        PanOsCefSniffRegex.IsMatch(rawText) || PanOsCsvSniffRegex.IsMatch(rawText);

    private static string ResolvePanOsProduct(string productHint) =>
        PanOsProducts.Contains(productHint) ? productHint : PanOsProductValue;

    private static (string Action, string Outcome, string? ActorUser, string? SrcIp, string? DstIp, int? DstPort, string? Protocol)
        ClassifyPanOs(string rawText)
    {
        if (PanOsConfigHeaderRegex.IsMatch(rawText))
        {
            var user = GetCefExtension(rawText, "suser");
            return ("rule_change", "unknown", user, GetCefExtension(rawText, "src"), null, null, null);
        }

        if (PanOsCefSniffRegex.IsMatch(rawText))
            return ClassifyPanOsCef(rawText);

        if (PanOsCsvSniffRegex.IsMatch(rawText))
            return ClassifyPanOsCsv(rawText);

        return ("unknown", "unknown", null, null, null, null, null);
    }

    private static (string Action, string Outcome, string? ActorUser, string? SrcIp, string? DstIp, int? DstPort, string? Protocol)
        ClassifyPanOsCef(string rawText)
    {
        var headerMatch = PanOsTrafficHeaderRegex.Match(rawText);
        var headerAction = headerMatch.Success ? headerMatch.Groups[1].Value : null;
        var extAction = GetCefExtension(rawText, "act");
        var actionToken = extAction ?? headerAction;

        var (action, outcome) = MapPanOsTrafficAction(actionToken);
        return (
            action,
            outcome,
            GetCefExtension(rawText, "suser"),
            GetCefExtension(rawText, "src"),
            GetCefExtension(rawText, "dst"),
            ParsePort(GetCefExtension(rawText, "dpt")),
            NormalizeProtocol(GetCefExtension(rawText, "proto")));
    }

    private static (string Action, string Outcome, string? ActorUser, string? SrcIp, string? DstIp, int? DstPort, string? Protocol)
        ClassifyPanOsCsv(string rawText)
    {
        var csvBody = ExtractCsvBody(rawText);
        if (csvBody == null)
            return ("unknown", "unknown", null, null, null, null, null);

        var parts = csvBody.Split(',');
        if (parts.Length < 9)
            return ("unknown", "unknown", null, null, null, null, null);

        var subtype = parts[4].Trim();
        var (action, outcome) = subtype is "drop" or "deny"
            ? ("denied_flow", "failure")
            : subtype is "end" or "start"
                ? ("allowed_flow", "success")
                : ("unknown", "unknown");

        var dstPort = parts.Length > 25 ? ParsePort(parts[25].Trim()) : null;
        return (action, outcome, null, parts[7].Trim(), parts[8].Trim(), dstPort, null);
    }

    private static string? ExtractCsvBody(string rawText)
    {
        var match = PanOsCsvSniffRegex.Match(rawText);
        if (!match.Success)
            return null;

        var idx = rawText.IndexOf(match.Value, StringComparison.Ordinal);
        if (idx < 0)
            return null;

        var start = rawText.LastIndexOf(' ', idx);
        if (start < 0)
            start = 0;
        else
            start++;

        return rawText[start..];
    }

    private static (string Action, string Outcome) MapPanOsTrafficAction(string? actionToken)
    {
        if (string.IsNullOrWhiteSpace(actionToken))
            return ("unknown", "unknown");

        if (IsDenyAction(actionToken) || string.Equals(actionToken, "drop", StringComparison.OrdinalIgnoreCase))
            return ("denied_flow", "failure");

        if (IsAllowAction(actionToken))
            return ("allowed_flow", "success");

        return ("unknown", "unknown");
    }

    private static string? GetCefExtension(string rawText, string key)
    {
        foreach (Match match in CefExtensionRegex.Matches(rawText))
        {
            if (string.Equals(match.Groups[1].Value, key, StringComparison.OrdinalIgnoreCase))
                return match.Groups[2].Value;
        }

        return null;
    }

    private static (string Action, string Outcome) ClassifyFortigateAction(string rawText)
    {
        if (IsFortigateRuleChange(rawText))
            return ("rule_change", "unknown");

        var action = MatchGroup(FortigateActionRegex, rawText);
        if (IsDenyAction(action))
            return ("denied_flow", "failure");

        if (IsAllowAction(action))
            return ("allowed_flow", "success");

        return ("unknown", "unknown");
    }

    private static bool IsFortigateRuleChange(string rawText)
    {
        var logType = MatchGroup(FortigateLogTypeRegex, rawText);
        if (!string.Equals(logType, "event", StringComparison.OrdinalIgnoreCase))
            return false;

        var cfgPath = MatchGroup(FortigateCfgPathRegex, rawText) ?? string.Empty;
        if (cfgPath.Contains("firewall", StringComparison.OrdinalIgnoreCase)
            || cfgPath.Contains("policy", StringComparison.OrdinalIgnoreCase))
            return true;

        var action = MatchGroup(FortigateActionRegex, rawText);
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

    [GeneratedRegex(@"%ASA-\d-", RegexOptions.CultureInvariant)]
    private static partial Regex AsaSniffPattern();

    [GeneratedRegex(
        @"Deny\s+(?<proto>\w+)\s+src\s+\w+:(?<srcIp>[\d.]+)/(?<srcPort>\d+)\s+dst\s+\w+:(?<dstIp>[\d.]+)/(?<dstPort>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AsaDenyPattern();

    [GeneratedRegex(
        @"Built\s+(?:outbound|inbound)\s+(?<proto>\w+)\s+connection\s+\d+\s+for\s+\w+:(?<srcIp>[\d.]+)/(?<srcPort>\d+).*?\s+to\s+\w+:(?<dstIp>[\d.]+)/(?<dstPort>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AsaBuiltPattern();

    [GeneratedRegex(@"User\s+'(?<user>[^']+)'\s+executed", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AsaConfigUserPattern();

    [GeneratedRegex(@"\bdevname=\S+.*\btype=""(traffic|event)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateSniffPattern();

    [GeneratedRegex(@"Palo\s+Alto\s+Networks\|PAN-OS\|", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PanOsCefSniffPattern();

    [GeneratedRegex(@",TRAFFIC,(drop|deny|end|start|allow),", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PanOsCsvSniffPattern();

    [GeneratedRegex(@"\|TRAFFIC\|(deny|drop|allow|alert|reset|end)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PanOsTrafficHeaderPattern();

    [GeneratedRegex(@"\|CONFIG\|", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PanOsConfigHeaderPattern();

    [GeneratedRegex(@"\bsrcip=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateSrcIpPattern();

    [GeneratedRegex(@"\bdstip=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateDstIpPattern();

    [GeneratedRegex(@"\bdstport=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateDstPortPattern();

    [GeneratedRegex(@"\bsrcport=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateSrcPortPattern();

    [GeneratedRegex(@"\bproto=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateProtoPattern();

    [GeneratedRegex(@"\baction=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateActionPattern();

    [GeneratedRegex(@"\btype=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateLogTypePattern();

    [GeneratedRegex(@"\bsubtype=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateLogSubtypePattern();

    [GeneratedRegex(@"\buser=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateUserPattern();

    [GeneratedRegex(@"\bcfgpath=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateCfgPathPattern();

    [GeneratedRegex(@"\bpolicyid=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigatePolicyIdPattern();

    [GeneratedRegex(@"\bservice=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateServicePattern();

    [GeneratedRegex(@"\bdevname=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateDevNamePattern();

    [GeneratedRegex(@"\blogid=(?:""([^""]+)""|(\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FortigateLogIdPattern();

    [GeneratedRegex(@"\b([a-zA-Z][\w-]*)=([^\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CefExtensionPattern();
}
