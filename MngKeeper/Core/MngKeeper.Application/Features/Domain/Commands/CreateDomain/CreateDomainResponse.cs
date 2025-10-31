namespace MngKeeper.Application.Features.Domain.Commands.CreateDomain
{
    public class CreateDomainResponse
    {
        public string DomainId { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string AdminUsername { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsSuccess { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}
