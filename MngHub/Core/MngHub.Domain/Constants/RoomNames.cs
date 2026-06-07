namespace MngHub.Domain.Constants;

/// <summary>
/// SignalR room naming constants
/// </summary>
public static class RoomNames
{
    /// <summary>
    /// Global room for system-wide announcements
    /// </summary>
    public const string Global = "global";

    /// <summary>
    /// Get domain-specific room name
    /// </summary>
    /// <param name="domainName">Domain name</param>
    /// <returns>Room name in format: "domain.{domainName}"</returns>
    public static string GetDomainRoom(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new ArgumentException("Domain name cannot be empty", nameof(domainName));
        }

        return $"domain.{domainName}";
    }

    /// <summary>
    /// User-targeted room for in-app / toast notifications (Keeper person id / mng_person_id).
    /// </summary>
    public static string GetUserRoom(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty", nameof(userId));

        return $"user:{userId.Trim()}";
    }
}

/// <summary>
/// RabbitMQ routing key patterns
/// </summary>
public static class RoutingKeyPatterns
{
    /// <summary>
    /// Global/system events pattern (all users)
    /// Matches: system.mngkeeper.domain.created, global.*
    /// Note: system.* only matches one segment, use system.# for multi-segment routing keys
    /// </summary>
    public const string Global = "global.*";
    public const string System = "system.#";  // Changed from system.* to system.# to match multi-segment routing keys

    /// <summary>
    /// Get domain-specific events pattern
    /// MngKeeper uses: {domainId}.{eventType} format
    /// We need to match any routing key that starts with domainId
    /// But we have domainName, not domainId, so we'll use a wildcard pattern
    /// </summary>
    /// <param name="domainName">Domain name</param>
    /// <returns>Routing key pattern: "domain.{domainName}.#" or "{domainId}.*"</returns>
    public static string GetDomainPattern(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new ArgumentException("Domain name cannot be empty", nameof(domainName));
        }

        // MngKeeper uses domainId in routing keys, but we have domainName
        // We'll subscribe to both patterns to be safe
        return $"domain.{domainName}.#";
    }

    /// <summary>
    /// Get domain-specific events pattern by domainId (MongoDB ObjectId)
    /// MngKeeper EventPublisher uses: {domainId}.{eventType} format
    /// </summary>
    /// <param name="domainId">Domain ID (MongoDB ObjectId)</param>
    /// <returns>Routing key pattern: "{domainId}.*"</returns>
    public static string GetDomainPatternById(string domainId)
    {
        if (string.IsNullOrWhiteSpace(domainId))
        {
            throw new ArgumentException("Domain ID cannot be empty", nameof(domainId));
        }

        return $"{domainId}.*";
    }
}

