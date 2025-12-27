using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Application.Services;

/// <summary>
/// Permission service for dataset access control
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Check if user has permission to perform operation on dataset
    /// </summary>
    /// <param name="schema">Dataset schema</param>
    /// <param name="permissionType">Permission type: read, create, update, delete</param>
    /// <param name="userGroups">User's group names from JWT token</param>
    /// <param name="domainName">User's domain name from JWT token</param>
    /// <returns>True if authorized, false otherwise</returns>
    bool CheckPermission(
        DatasetSchema schema,
        string permissionType,
        List<string> userGroups,
        string domainName);

    /// <summary>
    /// Get user groups from JWT token claims
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>List of group names</returns>
    List<string> GetUserGroups(Microsoft.AspNetCore.Http.HttpContext httpContext);
}

