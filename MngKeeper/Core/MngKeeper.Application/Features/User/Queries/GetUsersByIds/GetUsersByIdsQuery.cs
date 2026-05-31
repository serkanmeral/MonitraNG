using MediatR;

namespace MngKeeper.Application.Features.User.Queries.GetUsersByIds
{
    /// <summary>
    /// MO dizin çözümü için toplu kullanıcı sorgusu (N+1 yerine tek istek).
    /// Verilen id'ler Keeper <c>__dataId</c> veya Keycloak <c>sub</c> olabilir.
    /// </summary>
    public class GetUsersByIdsQuery : IRequest<GetUsersByIdsResponse>
    {
        public List<string> Ids { get; set; } = new();
    }

    public class GetUsersByIdsResponse
    {
        public List<UserLookupItemDto> Users { get; set; } = new();
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Dizin çözümü için yalın kullanıcı görünümü: ad/başlık/aktif + her iki kimlik (eşleme için).</summary>
    public class UserLookupItemDto
    {
        public string UserId { get; set; } = string.Empty;
        public string? KeycloakUserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Title { get; set; }
        public bool IsActive { get; set; }
    }
}
