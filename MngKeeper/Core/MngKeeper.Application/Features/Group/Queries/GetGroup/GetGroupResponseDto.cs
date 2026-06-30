using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Application.Features.Group.Queries.GetGroup
{
    public class GetGroupResponseDto
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IncludeInApplication { get; set; } = true;
        public int MemberCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }
        public string ProvisioningSource { get; set; } = "Local";
        public DateTime? DirectorySyncedAt { get; set; }
        public GroupCapabilitiesDto Capabilities { get; set; } = new();
    }
}
