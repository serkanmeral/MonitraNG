using System.Text.Json.Serialization;

namespace MngDocument.Application.Models;

public class DmResourceKind
{
    [JsonPropertyName("__dataId")]
    public string? __dataId { get; set; }

    public string? code { get; set; }
    public string? displayName { get; set; }
    public string? description { get; set; }
    public string? family { get; set; }
    public int? sortOrder { get; set; }
    public bool? isActive { get; set; }
}

public class DmRelationType
{
    [JsonPropertyName("__dataId")]
    public string? __dataId { get; set; }

    public string? code { get; set; }
    public string? displayName { get; set; }
    public string? description { get; set; }
    public string? appliesTo { get; set; }
    public int? sortOrder { get; set; }
    public bool? isActive { get; set; }
}
