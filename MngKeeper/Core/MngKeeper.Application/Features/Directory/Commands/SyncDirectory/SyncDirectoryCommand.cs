using MediatR;
using MngKeeper.Application.Directory;

namespace MngKeeper.Application.Features.Directory.Commands.SyncDirectory;

public class SyncDirectoryCommand : IRequest<DirectorySyncResult>
{
    public string DomainId { get; set; } = string.Empty;
    public DirectorySyncTrigger TriggeredBy { get; set; } = DirectorySyncTrigger.Manual;
}
