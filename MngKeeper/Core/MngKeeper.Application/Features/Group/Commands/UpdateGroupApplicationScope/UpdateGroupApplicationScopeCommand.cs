using MediatR;

namespace MngKeeper.Application.Features.Group.Commands.UpdateGroupApplicationScope;

public class UpdateGroupApplicationScopeCommand : IRequest<UpdateGroupApplicationScopeResponse>
{
    public string GroupId { get; set; } = string.Empty;
    public bool IncludeInApplication { get; set; }
}

public class UpdateGroupApplicationScopeResponse
{
    public string GroupId { get; set; } = string.Empty;
    public bool IncludeInApplication { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
