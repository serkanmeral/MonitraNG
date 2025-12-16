using MngKeeper.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MngKeeper.Application.Interfaces;

/// <summary>
/// DataGateway sync service - MngKeeper'dan MngDataGateway MongoDB'ye direkt sync
/// </summary>
public interface IDataGatewaySyncService
{
    /// <summary>
    /// Sync user to DataGateway MongoDB (mng_{domain} database)
    /// </summary>
    /// <param name="user">User entity from MngKeeper</param>
    /// <param name="domainId">Domain ID</param>
    /// <param name="customData">Custom data dictionary (optional)</param>
    Task SyncUserToDataGatewayAsync(
        User user, 
        string domainId,
        Dictionary<string, object>? customData = null);

    /// <summary>
    /// Sync group to DataGateway MongoDB (mng_{domain} database)
    /// </summary>
    /// <param name="group">Group entity from MngKeeper</param>
    /// <param name="domainId">Domain ID</param>
    /// <param name="customData">Custom data dictionary (optional)</param>
    Task SyncGroupToDataGatewayAsync(
        Group group, 
        string domainId,
        Dictionary<string, object>? customData = null);

    /// <summary>
    /// Sync all users for a domain (manual sync)
    /// </summary>
    /// <param name="domainId">Domain ID</param>
    /// <returns>Sync result with counts</returns>
    Task<DataGatewaySyncResult> SyncAllUsersAsync(string domainId);

    /// <summary>
    /// Sync all groups for a domain (manual sync)
    /// </summary>
    /// <param name="domainId">Domain ID</param>
    /// <returns>Sync result with counts</returns>
    Task<DataGatewaySyncResult> SyncAllGroupsAsync(string domainId);

    /// <summary>
    /// Sync both users and groups for a domain (manual sync)
    /// </summary>
    /// <param name="domainId">Domain ID</param>
    /// <returns>Combined sync result</returns>
    Task<DataGatewaySyncResult> SyncAllAsync(string domainId);
}

/// <summary>
/// DataGateway sync operation result
/// </summary>
public class DataGatewaySyncResult
{
    public int TotalCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsSuccess => ErrorCount == 0;
    public string Message { get; set; } = string.Empty;
}

