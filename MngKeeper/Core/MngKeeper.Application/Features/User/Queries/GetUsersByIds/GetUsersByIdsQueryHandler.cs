using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Features.User.Queries.GetUsersByIds
{
    public class GetUsersByIdsQueryHandler : IRequestHandler<GetUsersByIdsQuery, GetUsersByIdsResponse>
    {
        // Tek istekte aşırı yük olmaması için üst sınır (MO board sayfası tipik olarak çok daha azını ister).
        private const int MaxIds = 500;

        private readonly IUserRepository _userRepository;
        private readonly IDirectoryCache _directoryCache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<GetUsersByIdsQueryHandler> _logger;

        public GetUsersByIdsQueryHandler(
            IUserRepository userRepository,
            IDirectoryCache directoryCache,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GetUsersByIdsQueryHandler> logger)
        {
            _userRepository = userRepository;
            _directoryCache = directoryCache;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<GetUsersByIdsResponse> Handle(GetUsersByIdsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return new GetUsersByIdsResponse
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
                    return new GetUsersByIdsResponse { IsSuccess = true };

                // 1) Redis profil cache'inden çöz; 2) eksikleri Mongo'dan oku ve cache'le.
                var cached = await _directoryCache.GetUsersAsync(claims.DomainId, ids);
                var items = cached.Values.ToList();
                var missing = ids.Where(id => !cached.ContainsKey(id)).ToList();

                if (missing.Count > 0)
                {
                    var users = await _userRepository.GetByIdsAsync(missing, claims.DomainId);
                    var fresh = users
                        .Where(u => u.DomainId == claims.DomainId)
                        .Select(u => new UserLookupItemDto
                        {
                            UserId = u.Id,
                            KeycloakUserId = u.KeycloakUserId,
                            Username = u.Username,
                            Email = u.Email,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            Title = u.Title,
                            IsActive = u.IsActive
                        })
                        .ToList();

                    await _directoryCache.SetUsersAsync(claims.DomainId, fresh);
                    items.AddRange(fresh);
                }

                // İstenen id'lerde aynı kullanıcı hem __dataId hem sub ile gelebilir → UserId'e göre tekille.
                var deduped = items
                    .GroupBy(i => i.UserId, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .ToList();

                return new GetUsersByIdsResponse { Users = deduped, IsSuccess = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by ids (count={Count})", request.Ids?.Count ?? 0);
                return new GetUsersByIdsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to get users: {ex.Message}"
                };
            }
        }
    }
}
