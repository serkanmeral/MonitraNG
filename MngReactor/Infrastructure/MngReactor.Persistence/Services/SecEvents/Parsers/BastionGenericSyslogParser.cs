using System.Text.RegularExpressions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Jump host / bastion syslog — sshd session auth (B1 P2).</summary>
public sealed partial class BastionGenericSyslogParser : ISecEventParser
{
    public const string ParserIdValue = "bastion.generic.v1";

    private static readonly HashSet<string> BastionProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        "bastion",
        "jump-host",
        "jumpserver",
        "jump-host-syslog"
    };

    private static readonly Regex FailedPasswordRegex = FailedPasswordPattern();
    private static readonly Regex AcceptedPasswordRegex = AcceptedPasswordPattern();
    private static readonly Regex AcceptedPublicKeyRegex = AcceptedPublicKeyPattern();
    private static readonly Regex SessionOpenedRegex = SessionOpenedPattern();

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        var type = SecEventParseHelpers.NormalizeType(raw.Source.Type);
        return type.Equals("bastion", StringComparison.OrdinalIgnoreCase)
               || BastionProducts.Contains(product);
    }

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        var (action, outcome, user, srcIp) = Classify(rawText);

        return new ParsedSecEvent
        {
            Timestamp = raw.ReceivedAt,
            EventAction = action,
            EventOutcome = outcome,
            ActorUser = user,
            NetworkSrcIp = srcIp,
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "bastion"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, "bastion"),
            SourceHost = raw.Source.Host,
            ParserId = ParserId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText)
        };
    }

    private static (string Action, string Outcome, string? User, string? SrcIp) Classify(string rawText)
    {
        var failed = FailedPasswordRegex.Match(rawText);
        if (failed.Success)
            return ("login_failed", "failure", failed.Groups["user"].Value, failed.Groups["ip"].Value);

        var acceptedPwd = AcceptedPasswordRegex.Match(rawText);
        if (acceptedPwd.Success)
            return ("login_success", "success", acceptedPwd.Groups["user"].Value, acceptedPwd.Groups["ip"].Value);

        var acceptedKey = AcceptedPublicKeyRegex.Match(rawText);
        if (acceptedKey.Success)
            return ("login_success", "success", acceptedKey.Groups["user"].Value, acceptedKey.Groups["ip"].Value);

        var session = SessionOpenedRegex.Match(rawText);
        if (session.Success)
            return ("session_opened", "success", session.Groups["user"].Value, session.Groups["ip"].Value);

        return ("unknown", "unknown", null, null);
    }

    [GeneratedRegex(
        @"Failed password for (?:invalid user )?(?<user>\S+) from (?<ip>[\d.]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FailedPasswordPattern();

    [GeneratedRegex(
        @"Accepted password for (?<user>\S+) from (?<ip>[\d.]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AcceptedPasswordPattern();

    [GeneratedRegex(
        @"Accepted publickey for (?<user>\S+) from (?<ip>[\d.]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AcceptedPublicKeyPattern();

    [GeneratedRegex(
        @"session opened for user (?<user>\S+)(?:\(uid=\d+\))? by \(uid=\d+\)(?: from (?<ip>[\d.]+))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SessionOpenedPattern();
}
