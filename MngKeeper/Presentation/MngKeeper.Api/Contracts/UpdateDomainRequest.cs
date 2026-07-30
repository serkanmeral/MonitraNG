using MngKeeper.Domain.Entities;

namespace MngKeeper.Api.Contracts;

/// <summary>
/// Partial domain update from UI. Nested settings are merged; omitted privilege/LDAP
/// blocks must not wipe existing Mongo values.
/// </summary>
public class UpdateDomainRequest
{
    public string? DisplayName { get; set; }
    public string? RelatedPersonPhone { get; set; }
    public string? Logo { get; set; }
    public string? LogoUrl { get; set; }
    public UpdateDomainSettingsRequest? Settings { get; set; }
}

public class UpdateDomainSettingsRequest
{
    public int? MaxUsers { get; set; }
    public int? MaxAssets { get; set; }
    public bool? EnableMqtt { get; set; }

    /// <summary>When null/absent, existing directoryPrivileges are kept.</summary>
    public DirectoryPrivilegeSettings? DirectoryPrivileges { get; set; }

    /// <summary>When null/absent, existing directoryLdap is kept.</summary>
    public DirectoryLdapSettings? DirectoryLdap { get; set; }
}
