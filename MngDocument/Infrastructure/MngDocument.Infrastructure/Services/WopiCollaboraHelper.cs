using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

internal static class WopiCollaboraHelper
{
    public static string? NormalizePostMessageOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return null;

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            return null;

        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static string? ResolvePostMessageOrigin(WopiSession session, CollaboraSettings settings)
    {
        var fromSession = NormalizePostMessageOrigin(session.PostMessageOrigin);
        if (!string.IsNullOrEmpty(fromSession))
            return fromSession;

        var configured = settings.PostMessageOrigin?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }
}
