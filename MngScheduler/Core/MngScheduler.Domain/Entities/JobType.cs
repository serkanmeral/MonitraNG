namespace MngScheduler.Domain.Entities;

/// <summary>
/// Job type enumeration
/// </summary>
public enum JobType
{
    /// <summary>
    /// System job - Admin only, stored in mng_keeper database
    /// </summary>
    System = 0,
    
    /// <summary>
    /// User job - Domain-based, stored in domain database via MngDataGateway
    /// </summary>
    User = 1
}
