using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Features.Group.Queries.GetGroupsByIds
{
    public class GetGroupsByIdsQueryHandler : IRequestHandler<GetGroupsByIdsQuery, GetGroupsByIdsResponse>
    {
        private const int MaxIds = 500;

        private readonly IGroupRepository _groupRepository;
        private readonly IDirectoryCache _directoryCache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<GetGroupsByIdsQueryHandler> _logger;

        public GetGroupsByIdsQueryHandler(
            IGroupRepository groupRepository,
            IDirectoryCache directoryCache,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GetGroupsByIdsQueryHandler> logger)
        {
            _groupRepository = groupRepository;
            _directoryCache = directoryCache;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<GetGroupsByIdsResponse> Handle(GetGroupsByIdsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return new GetGroupsByIdsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                var ids = (request.Ids ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaxIds)
                    .ToList();

                if (ids.Count == 0)
                    return new GetGroupsByIdsResponse { IsSuccess = true };

                // 1) Redis profil cache'inden çöz; 2) eksikleri Mongo'dan oku ve cache'le.
                var cached = await _directoryCache.GetGroupsAsync(claims.DomainId, ids);
                var items = cached.Values.ToList();
                var missing = ids.Where(id => !cached.ContainsKey(id)).ToList();

                if (missing.Count > 0)
                {
                    var groups = await _groupRepository.GetByIdsAsync(missing, claims.DomainId);
                    var fresh = groups
                        .Where(g => g.DomainId == claims.DomainId)
                        .Select(g => new GroupLookupItemDto
                        {
                            GroupId = g.Id,
                            Name = g.Name,
                            IsActive = g.IsActive
                        })
                        .ToList();

                    await _directoryCache.SetGroupsAsync(claims.DomainId, fresh);
                    items.AddRange(fresh);
                }

                var deduped = items
                    .GroupBy(i => i.GroupId, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();

                return new GetGroupsByIdsResponse { Groups = deduped, IsSuccess = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups by ids (count={Count})", request.Ids?.Count ?? 0);
                return new GetGroupsByIdsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to get groups: {ex.Message}"
                };
            }
        }
    }
}
