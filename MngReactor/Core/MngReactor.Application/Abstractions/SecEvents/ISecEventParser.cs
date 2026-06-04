using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventParser
{
    string ParserId { get; }
    bool CanParse(SecEventRawContext raw);
    ParsedSecEvent Parse(SecEventRawContext raw);
}
