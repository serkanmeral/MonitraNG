using Microsoft.Extensions.Logging;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventIngestProcessing : ISecEventIngestProcessing
{
    private readonly ILogger<SecEventIngestProcessing> _logger;
    private readonly ISecEventParserRegistry _registry;
    private readonly UnknownSecEventFallback _fallback;
    private readonly ISecEventsRepository _repository;
    private readonly ISecEventPublisher _publisher;

    public SecEventIngestProcessing(
        ILogger<SecEventIngestProcessing> logger,
        ISecEventParserRegistry registry,
        UnknownSecEventFallback fallback,
        ISecEventsRepository repository,
        ISecEventPublisher publisher)
    {
        _logger = logger;
        _registry = registry;
        _fallback = fallback;
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<SecEventIngestResponse> ProcessAsync(
        SecEventIngestRequest request,
        string domainFromToken,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        var items = request.Items ?? [];
        if (items.Count > SecEventIngestLimits.MaxItemsPerRequest)
        {
            return new SecEventIngestResponse
            {
                Accepted = 0,
                Rejected = items.Count,
                Published = 0,
                Message = $"Batch exceeds max items ({SecEventIngestLimits.MaxItemsPerRequest})."
            };
        }

        if (string.IsNullOrWhiteSpace(domainFromToken))
        {
            return new SecEventIngestResponse
            {
                Accepted = 0,
                Rejected = items.Count,
                Published = 0,
                Message = "Domain required."
            };
        }

        if (items.Count == 0)
        {
            return new SecEventIngestResponse
            {
                Accepted = 0,
                Rejected = 0,
                Published = 0
            };
        }

        var domain = domainFromToken.Trim();
        var ingestedAt = DateTime.UtcNow;
        var docs = new List<SecEventDocument>(items.Count);
        var messages = new List<SecEventCreatedMessage>(items.Count);

        foreach (var item in items)
        {
            var ctx = SecEventRawContext.From(item);
            var parsed = ParseSafe(ctx);
            docs.Add(SecEventDocument.FromParsed(parsed, domain, ingestedAt));
            messages.Add(ToCreatedMessage(domain, parsed));
        }

        var inserted = await _repository.InsertManyAsync(domain, docs, cancellationToken);

        _ = _publisher.PublishCreatedAsync(domain, messages, cancellationToken);

        return new SecEventIngestResponse
        {
            Accepted = inserted,
            Rejected = items.Count - inserted,
            Published = messages.Count
        };
    }

    private ParsedSecEvent ParseSafe(SecEventRawContext ctx)
    {
        try
        {
            return _registry.Resolve(ctx).Parse(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "sec_events parse failed, using fallback parserId={ParserId}", _fallback.ParserId);
            return _fallback.Parse(ctx);
        }
    }

    private static SecEventCreatedMessage ToCreatedMessage(string domain, ParsedSecEvent parsed) =>
        new()
        {
            Domain = domain,
            Timestamp = parsed.Timestamp,
            EventAction = parsed.EventAction,
            NetworkSrcIp = parsed.NetworkSrcIp,
            SourceType = parsed.SourceType,
            ParserId = parsed.ParserId
        };
}
