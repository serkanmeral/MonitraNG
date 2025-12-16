namespace MngKeeper.Application.Common.Constants
{
    /// <summary>
    /// System-wide constants for MngKeeper application
    /// </summary>
    public static class SystemConstants
    {
        /// <summary>
        /// Default system user identifier for audit fields
        /// </summary>
        public const string SystemUser = "system";

        /// <summary>
        /// Default database name
        /// </summary>
        public const string DefaultDatabaseName = "MngKeeper";

        /// <summary>
        /// Default collection names
        /// </summary>
        public static class Collections
        {
            public const string Domains = "domains";
            public const string Users = "users";
            public const string Groups = "groups";
            public const string DataGatewayUsers = "@users";
            public const string DataGatewayGroups = "@groups";
        }

        /// <summary>
        /// Default pagination settings
        /// </summary>
        public static class Pagination
        {
            public const int DefaultPage = 1;
            public const int DefaultPageSize = 20;
            public const int MaxPageSize = 100;
        }

        /// <summary>
        /// Cache TTL settings (in minutes)
        /// </summary>
        public static class Cache
        {
            public const int UsersList = 5;
            public const int GroupsList = 5;
            public const int UserDetails = 10;
            public const int GroupDetails = 10;
            public const int DomainDetails = 15;
        }
    }

    /// <summary>
    /// System group names constants
    /// </summary>
    public static class SystemGroups
    {
        public const string Admins = "admins";
        public const string Managers = "managers";
        public const string Users = "users";
        public const string Guests = "guests";

        /// <summary>
        /// All default system groups
        /// </summary>
        public static readonly string[] All = { Admins, Managers, Users, Guests };

        /// <summary>
        /// Check if a group name is a system group
        /// </summary>
        public static bool IsSystemGroup(string groupName)
        {
            return All.Contains(groupName, StringComparer.OrdinalIgnoreCase);
        }
    }
}

