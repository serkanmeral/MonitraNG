using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventParserRegistry
{
    ISecEventParser Resolve(SecEventRawContext raw);
}
