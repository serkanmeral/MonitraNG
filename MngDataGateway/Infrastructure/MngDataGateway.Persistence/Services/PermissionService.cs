using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// Permission service implementation for dataset access control
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(ILogger<PermissionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check if user has permission to perform operation on dataset
    /// </summary>
    public bool CheckPermission(
        DatasetSchema schema,
        string permissionType,
        List<string> userGroups,
        string domainName)
    {
        // If no permissions defined, everyone can access
        if (schema.permissions == null)
        {
            _logger.LogDebug("Dataset {DatasetName} has no permissions defined, allowing access", schema.name);
            return true;
        }

        // Get permission definition for the requested operation
        PermissionDefinition? permissionDef = permissionType.ToLower() switch
        {
            "read" => schema.permissions.read,
            "create" => schema.permissions.create,
            "update" => schema.permissions.update,
            "delete" => schema.permissions.delete,
            _ => null
        };

        // If permission not defined for this operation, everyone can access
        if (permissionDef == null)
        {
            _logger.LogDebug("Permission type {PermissionType} not defined for dataset {DatasetName}, allowing access", 
                permissionType, schema.name);
            return true;
        }

        // If groups array is empty, no one is authorized
        if (permissionDef.groups == null || permissionDef.groups.Count == 0)
        {
            _logger.LogWarning("Permission type {PermissionType} for dataset {DatasetName} has empty groups array, denying access", 
                permissionType, schema.name);
            return false;
        }

        // Check if user has at least one of the required groups
        var hasPermission = userGroups.Any(group => permissionDef.groups.Contains(group, StringComparer.OrdinalIgnoreCase));

        if (!hasPermission)
        {
            _logger.LogWarning(
                "User groups {UserGroups} do not have {PermissionType} permission for dataset {DatasetName}. Required groups: {RequiredGroups}", 
                string.Join(", ", userGroups), permissionType, schema.name, string.Join(", ", permissionDef.groups));
        }
        else
        {
            _logger.LogDebug(
                "User has {PermissionType} permission for dataset {DatasetName} (groups: {UserGroups})", 
                permissionType, schema.name, string.Join(", ", userGroups));
        }

        return hasPermission;
    }

    /// <summary>
    /// Get user groups from JWT token claims
    /// </summary>
    public List<string> GetUserGroups(HttpContext httpContext)
    {
        var user = httpContext.User;
        var userGroups = new List<string>();

        // Try to get user_groups claim
        var userGroupsClaim = user.FindFirst("user_groups");

        if (userGroupsClaim != null && !string.IsNullOrWhiteSpace(userGroupsClaim.Value))
        {
            try
            {
                // Try to parse as JSON array first
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(userGroupsClaim.Value);
                
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    // It's a JSON array
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            var groupName = element.GetString();
                            if (!string.IsNullOrWhiteSpace(groupName))
                            {
                                userGroups.Add(groupName);
                            }
                        }
                    }
                }
                else if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    // Single string value
                    var groupName = jsonElement.GetString();
                    if (!string.IsNullOrWhiteSpace(groupName))
                    {
                        userGroups.Add(groupName);
                    }
                }
            }
            catch (JsonException)
            {
                // If JSON parsing fails, try as comma-separated string
                var groups = userGroupsClaim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                userGroups.AddRange(groups.Select(g => g.Trim()));
            }
        }

        // Remove empty strings
        userGroups = userGroups.Where(g => !string.IsNullOrWhiteSpace(g)).ToList();

        _logger.LogDebug("Extracted user groups from token: {Groups}", string.Join(", ", userGroups));

        return userGroups;
    }
}

