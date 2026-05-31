using MediatR;

namespace MngKeeper.Application.Features.Group.Queries.GetGroupsByIds
{
    /// <summary>MO dizin çözümü için toplu grup sorgusu (N+1 yerine tek istek). Id'ler Keeper <c>__dataId</c>.</summary>
    public class GetGroupsByIdsQuery : IRequest<GetGroupsByIdsResponse>
    {
        public List<string> Ids { get; set; } = new();
    }

    public class GetGroupsByIdsResponse
    {
        public List<GroupLookupItemDto> Groups { get; set; } = new();
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Dizin çözümü için yalın grup görünümü: id + ad + aktif.</summary>
    public class GroupLookupItemDto
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
