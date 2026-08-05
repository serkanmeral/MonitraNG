using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Features.Domain.Commands.CreateDomain;
using DomainEntity = MngKeeper.Domain.Entities.Domain;

namespace MngKeeper.Application.Pipelines.DomainCreation;

/// <summary>
/// Context for domain creation pipeline
/// Carries state through all pipeline steps
/// </summary>
public class DomainCreationContext
{
    // Input
    public string DomainName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? DiscoveryRootLabel { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public DomainSettingsDto Settings { get; set; } = new();
    public string? RelatedPersonPhone { get; set; }
    public string? RelatedPersonEmail { get; set; }
    public string? Logo { get; set; }
    public string? LogoUrl { get; set; }
    
    // Template Selection
    public string? TemplateName { get; set; }  // Template name to use for initial data (optional)
    
    // Generated/Computed
    public string DatabaseName { get; set; } = string.Empty;
    public string RealmName { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    
    // Created Entities
    public DomainEntity? Domain { get; set; }
    public RealmInfo? RealmInfo { get; set; }
    public GroupInfo? AdminsGroup { get; set; }
    public GroupInfo? ManagersGroup { get; set; }
    public GroupInfo? UsersGroup { get; set; }
    public GroupInfo? GuestsGroup { get; set; }
    public UserInfo? AdminUser { get; set; }
    
    // Tracking
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

