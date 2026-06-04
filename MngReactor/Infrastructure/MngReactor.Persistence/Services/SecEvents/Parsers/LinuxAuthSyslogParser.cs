using System.Text.RegularExpressions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Linux auth syslog — sshd / sudo (B1 P1).</summary>
public sealed partial class LinuxAuthSyslogParser : ISecEventParser
{
    public const string ParserIdValue = "linux.auth.v1";

    private static readonly Regex FailedPasswordRegex = FailedPasswordPattern();
    private static readonly Regex AcceptedPasswordRegex = AcceptedPasswordPattern();
    private static readonly Regex SudoDeniedRegex = SudoDeniedPattern();

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        var type = SecEventParseHelpers.NormalizeType(raw.Source.Type);
        if (product.Equals("linux-syslog", StringComparison.OrdinalIgnoreCase)
            || product.Equals("linux-auth", StringComparison.OrdinalIgnoreCase)
            || type.Equals("endpoint", StringComparison.OrdinalIgnoreCase)
            || type.Equals("linux", StringComparison.OrdinalIgnoreCase))
            return true;

        var text = SecEventParseHelpers.GetRawText(raw.Raw);
        return !string.IsNullOrWhiteSpace(text)
               && (text.Contains("sshd[", StringComparison.Ordinal)
                   || text.Contains("sudo:", StringComparison.Ordinal));
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
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "endpoint"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, "linux-syslog"),
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
        {
            return (
                "login_failed",
                "failure",
                failed.Groups["user"].Value,
                failed.Groups["ip"].Value);
        }

        var accepted = AcceptedPasswordRegex.Match(rawText);
        if (accepted.Success)
        {
            return (
                "login_success",
                "success",
                accepted.Groups["user"].Value,
                accepted.Groups["ip"].Value);
        }

        if (SudoDeniedRegex.IsMatch(rawText))
        {
            var sudoUser = SudoDeniedRegex.Match(rawText).Groups["user"].Value;
            return ("privilege_denied", "failure", sudoUser, null);
        }

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
        @"sudo:\s+(?<user>\S+)\s+:\s+command not allowed",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SudoDeniedPattern();
}
