using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Features.Group.Commands.UpdateGroupApplicationScope;

public class UpdateGroupApplicationScopeCommandHandler
    : IRequestHandler<UpdateGroupApplicationScopeCommand, UpdateGroupApplicationScopeResponse>
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDomainRepository _domainRepository;
    private readonly IDataGatewaySyncService _dataGatewaySyncService;
    private readonly IDirectoryCache _directoryCache;
    private readonly ILogger<UpdateGroupApplicationScopeCommandHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UpdateGroupApplicationScopeCommandHandler(
        IGroupRepository groupRepository,
        IDomainRepository domainRepository,
        IDataGatewaySyncService dataGatewaySyncService,
        IDirectoryCache directoryCache,
        ILogger<UpdateGroupApplicationScopeCommandHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _groupRepository = groupRepository;
        _domainRepository = domainRepository;
        _dataGatewaySyncService = dataGatewaySyncService;
        _directoryCache = directoryCache;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<UpdateGroupApplicationScopeResponse> Handle(
        UpdateGroupApplicationScopeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
            if (claims?.DomainId == null)
            {
                return new UpdateGroupApplicationScopeResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Domain information not found in token."
                };
            }

            var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
            if (domain == null)
            {
                return new UpdateGroupApplicationScopeResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Domain not found."
                };
            }

            var group = await _groupRepository.GetByIdAsync(request.GroupId, claims.DomainId);
            if (group == null || group.DomainId != claims.DomainId)
            {
                return new UpdateGroupApplicationScopeResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Group not found."
                };
            }

            group.IncludeInApplication = request.IncludeInApplication;
            group.UpdatedAt = DateTime.UtcNow;
            group.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;

            var updated = await _groupRepository.UpdateAsync(group);
            await _directoryCache.InvalidateGroupAsync(claims.DomainId, updated.Id);

            try
            {
                await _dataGatewaySyncService.SyncGroupToDataGatewayAsync(updated, claims.DomainId, null);
            }
            catch (Exception syncEx)
            {
                _logger.LogError(syncEx, "Failed to sync group application scope to DataGateway: GroupId={GroupId}", updated.Id);
            }

            _logger.LogInformation(
                "Group application scope updated: GroupId={GroupId} IncludeInApplication={Include}",
                updated.Id, updated.IncludeInApplication);

            return new UpdateGroupApplicationScopeResponse
            {
                GroupId = updated.Id,
                IncludeInApplication = updated.IncludeInApplication,
                UpdatedAt = updated.UpdatedAt ?? DateTime.UtcNow,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group application scope: {GroupId}", request.GroupId);
            return new UpdateGroupApplicationScopeResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
