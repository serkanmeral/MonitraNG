using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

/// <summary>Applies published parse-rule catalog before C# ISecEventParser fallback.</summary>
public interface ISecEventCatalogParseEngine
{
    /// <summary>
    /// Returns a parsed event when an enabled catalog rule matches and sets a non-unknown action;
    /// otherwise null (caller should use code parsers).
    /// </summary>
    Task<ParsedSecEvent?> TryParseAsync(
        string domain,
        SecEventRawContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Published enabled rules cache (invalidated on Publish).</summary>
public interface ISecEventParseRuleCatalogCache
{
    Task<IReadOnlyList<SecEventParseRuleDocument>> GetEnabledRulesAsync(
        string domain,
        CancellationToken cancellationToken = default);

    void Invalidate(string domain);
}
