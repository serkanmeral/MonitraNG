using MediatR;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Features.Directory.Commands.SyncDirectory;

public class SyncDirectoryCommandHandler : IRequestHandler<SyncDirectoryCommand, DirectorySyncResult>
{
    private readonly IDirectorySyncCoordinator _coordinator;
    private readonly IKeycloakToMongoSyncService _syncService;
    private readonly ILogger<SyncDirectoryCommandHandler> _logger;

    public SyncDirectoryCommandHandler(
        IDirectorySyncCoordinator coordinator,
        IKeycloakToMongoSyncService syncService,
        ILogger<SyncDirectoryCommandHandler> logger)
    {
        _coordinator = coordinator;
        _syncService = syncService;
        _logger = logger;
    }

    public async Task<DirectorySyncResult> Handle(SyncDirectoryCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DomainId))
        {
            return new DirectorySyncResult
            {
                IsSuccess = false,
                Code = "invalid_request",
                Message = "DomainId is required.",
                TriggeredBy = request.TriggeredBy.ToString()
            };
        }

        if (!_coordinator.TryBeginSync(request.DomainId))
        {
            _logger.LogWarning(
                "[DirectorySync] Coordinator lock denied domain={DomainId} trigger={Trigger}",
                request.DomainId, request.TriggeredBy);
            return new DirectorySyncResult
            {
                IsSuccess = false,
                Code = "sync_in_progress",
                Message = "A directory sync is already running for this domain.",
                DomainId = request.DomainId,
                TriggeredBy = request.TriggeredBy.ToString()
            };
        }

        try
        {
            _logger.LogInformation(
                "[DirectorySync] Pipeline starting domain={DomainId} trigger={Trigger}",
                request.DomainId, request.TriggeredBy);
            return await _syncService.SyncDomainAsync(request.DomainId, request.TriggeredBy, cancellationToken);
        }
        finally
        {
            _coordinator.EndSync(request.DomainId);
            _logger.LogDebug(
                "[DirectorySync] Coordinator lock released domain={DomainId}",
                request.DomainId);
        }
    }
}
