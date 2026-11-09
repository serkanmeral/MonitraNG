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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var userGroupsClaim in user.FindAll("user_groups"))
            AddGroupValues(userGroups, seen, userGroupsClaim.Value);

        _logger.LogDebug("Extracted user groups from token: {Groups}", string.Join(", ", userGroups));

        return userGroups;
    }

    private static void AddGroupValues(List<string> groups, HashSet<string> seen, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(trimmed);
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in jsonElement.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String)
                            AddGroup(groups, seen, element.GetString());
                    }
                    return;
                }

                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    AddGroup(groups, seen, jsonElement.GetString());
                    return;
                }
            }
            catch (JsonException)
            {
            }
        }

        foreach (var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            AddGroup(groups, seen, part);
    }

    private static void AddGroup(List<string> groups, HashSet<string> seen, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var trimmed = name.Trim();
        if (seen.Add(trimmed))
            groups.Add(trimmed);
    }
}

