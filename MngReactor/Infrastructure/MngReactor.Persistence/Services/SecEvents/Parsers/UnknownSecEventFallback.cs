using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Parse başarısız / eşleşmeyen ham olay — orchestrator catch ile de kullanılır.</summary>
public sealed class UnknownSecEventFallback : ISecEventParser
{
    public const string ParserIdValue = "unknown.fallback.v1";

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw) => true;

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        return new ParsedSecEvent
        {
            Timestamp = raw.ReceivedAt,
            EventAction = "unknown",
            EventOutcome = "unknown",
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "unknown"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, "unknown"),
            SourceHost = raw.Source.Host,
            ParserId = ParserId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText)
        };
    }
}
