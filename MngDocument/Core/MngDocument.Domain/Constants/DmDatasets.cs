namespace MngDocument.Domain.Constants;

/// <summary>
/// Document Intelligence (DM) dataset adları — DG koleksiyon adı = dataset adı.
/// OC <c>op_*</c> / TM <c>tm_*</c> deseniyle aynı: modül öneki <c>dm_</c>.
/// </summary>
public static class DmDatasets
{
    public const string Resources = "dm_resources";
    public const string ResourceVersions = "dm_resource_versions";
    public const string ResourcePermissions = "dm_resource_permissions";
    public const string ResourceLinks = "dm_resource_links";
}
