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
    public const string TemplateCategories = "dm_template_categories";
    public const string DocumentTemplates = "dm_document_templates";
    public const string Letterheads = "dm_letterheads";
    public const string CoverPages = "dm_cover_pages";
    public const string Tags = "dm_tags";
    public const string ResourceKinds = "dm_resource_kinds";
    public const string RelationTypes = "dm_relation_types";
    public const string GenerationCounters = "dm_generation_counters";
}
