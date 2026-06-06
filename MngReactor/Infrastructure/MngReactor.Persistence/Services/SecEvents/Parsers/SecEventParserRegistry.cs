using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

public sealed class SecEventParserRegistry : ISecEventParserRegistry
{
    private readonly IReadOnlyList<ISecEventParser> _parsers;
    private readonly UnknownSecEventFallback _fallback;

    public SecEventParserRegistry(
        WindowsSecurityExtendedParser windowsExtended,
        WindowsNxlogJsonParser windowsNxlog,
        WindowsSecurityParser windows,
        BastionGenericSyslogParser bastion,
        LinuxAuthSyslogParser linuxAuth,
        FirewallVendorParser firewallVendor,
        FirewallGenericSyslogParser firewall,
        UnknownSecEventFallback fallback)
    {
        _parsers = [windowsExtended, windowsNxlog, windows, bastion, linuxAuth, firewallVendor, firewall];
        _fallback = fallback;
    }

    public ISecEventParser Resolve(SecEventRawContext raw)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(raw))
                return parser;
        }

        return _fallback;
    }
}
