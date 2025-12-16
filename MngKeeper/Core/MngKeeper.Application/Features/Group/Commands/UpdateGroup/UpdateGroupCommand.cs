using MediatR;

namespace MngKeeper.Application.Features.Group.Commands.UpdateGroup
{
    public class UpdateGroupCommand : IRequest<UpdateGroupResponse>
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Custom data dictionary - MngDataGateway MongoDB'ye sync edilecek
        /// Keycloak'a yazılmaz, sadece DataGateway'de tutulur
        /// </summary>
        public Dictionary<string, object>? CustomData { get; set; }
    }

    public class UpdateGroupResponse
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
