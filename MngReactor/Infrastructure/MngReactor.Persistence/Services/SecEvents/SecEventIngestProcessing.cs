using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Observations;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Application.Observations;
using MngReactor.Application.Services.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventIngestProcessing : ISecEventIngestProcessing
{
    private readonly ILogger<SecEventIngestProcessing> _logger;
    private readonly ISecEventCatalogParseEngine _catalogEngine;
    private readonly ISecEventParserRegistry _registry;
    private readonly UnknownSecEventFallback _fallback;
    private readonly ISecEventsRepository _repository;
    private readonly ISecEventPublisher _publisher;
    private readonly IObservationPublisher _observationPublisher;
    private readonly ISecEventFlowBaselineStore _flowBaselineStore;
    private readonly SecEventsSettings _settings;

    public SecEventIngestProcessing(
        ILogger<SecEventIngestProcessing> logger,
        ISecEventCatalogParseEngine catalogEngine,
        ISecEventParserRegistry registry,
        UnknownSecEventFallback fallback,
        ISecEventsRepository repository,
        ISecEventPublisher publisher,
        IObservationPublisher observationPublisher,
        ISecEventFlowBaselineStore flowBaselineStore,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _catalogEngine = catalogEngine;
        _registry = registry;
        _fallback = fallback;
        _repository = repository;
        _publisher = publisher;
        _observationPublisher = observationPublisher;
        _flowBaselineStore = flowBaselineStore;
        _settings = options?.Value?.SecEvents ?? new SecEventsSettings();
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
        var skipped = 0;

        foreach (var item in items)
        {
            if (SecEventNxlogIngestGuard.ShouldReject(item, _settings.AcceptNxlogIngest))
            {
                skipped++;
                _logger.LogDebug(
                    "sec_events NXLog rejected domain={Domain} product={Product} host={Host} AcceptNxlogIngest=false",
                    domain,
                    item.Source?.Product,
                    item.Source?.Host);
                continue;
            }

            if (SecEventLinuxSyslogIngestGuard.ShouldReject(item, _settings.AcceptLinuxSyslogIngest))
            {
                skipped++;
                _logger.LogDebug(
                    "sec_events Linux syslog rejected domain={Domain} product={Product} host={Host} AcceptLinuxSyslogIngest=false",
                    domain,
                    item.Source?.Product,
                    item.Source?.Host);
                continue;
            }

            var ctx = SecEventRawContext.From(item);
            var parsed = await ParseSafeAsync(domain, ctx, cancellationToken);
            if (_settings.DropUnknownEvents && SecEventUnknownFilter.IsUnknown(parsed))
            {
                skipped++;
                _logger.LogDebug(
                    "sec_events unknown dropped domain={Domain} parser={ParserId} host={Host}",
                    domain,
                    parsed.ParserId,
                    parsed.SourceHost);
                continue;
            }

            var enrichment = await SecEventFlowBaselineEnricher.EnrichAsync(
                parsed, domain, _flowBaselineStore, cancellationToken);
            parsed = enrichment.Parsed;
            docs.Add(SecEventDocument.FromParsed(
                parsed,
                domain,
                ingestedAt,
                enrichment.EmitNewFlowObservation,
                _settings.PersistFullRaw));
            messages.Add(ToCreatedMessage(domain, parsed));
        }

        var inserted = docs.Count == 0
            ? 0
            : await _repository.InsertManyAsync(domain, docs, cancellationToken);

        if (messages.Count > 0)
            _ = _publisher.PublishCreatedAsync(domain, messages, cancellationToken);

        foreach (var doc in docs)
        {
            var observation = SecEventObservationMapper.ToPayload(doc, domain, domain);
            _ = _observationPublisher.PublishSecEventAsync(observation, cancellationToken);

            if (doc.BaselineNewFlowPair)
            {
                var newFlowObservation = SecEventObservationMapper.ToNewFlowPayload(observation);
                _ = _observationPublisher.PublishSecEventAsync(newFlowObservation, cancellationToken);
            }
        }

        return new SecEventIngestResponse
        {
            Accepted = inserted,
            Rejected = items.Count - inserted - skipped,
            Skipped = skipped,
            Published = messages.Count
        };
    }

    private async Task<ParsedSecEvent> ParseSafeAsync(
        string domain,
        SecEventRawContext ctx,
        CancellationToken cancellationToken)
    {
        try
        {
            var fromCatalog = await _catalogEngine.TryParseAsync(domain, ctx, cancellationToken);
            if (fromCatalog is not null)
                return fromCatalog;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "sec_events catalog parse failed domain={Domain}", domain);
        }

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
