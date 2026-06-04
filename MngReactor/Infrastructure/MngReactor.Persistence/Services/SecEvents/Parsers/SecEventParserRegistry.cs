using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

public sealed class SecEventParserRegistry : ISecEventParserRegistry
{
    private readonly IReadOnlyList<ISecEventParser> _parsers;
    private readonly UnknownSecEventFallback _fallback;

    public SecEventParserRegistry(
        WindowsSecurityParser windows,
        LinuxAuthSyslogParser linuxAuth,
        FirewallVendorParser firewallVendor,
        FirewallGenericSyslogParser firewall,
        UnknownSecEventFallback fallback)
    {
        _parsers = [windows, linuxAuth, firewallVendor, firewall];
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
