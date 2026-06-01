using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngDocument.Application.Models;

/// <summary>
/// <c>dm_resources</c> DG kaydının tip karşılığı (okuma). Yazmada Dictionary payload kullanılır.
/// Alan adları DG koleksiyon alan adlarıyla birebir (camelCase / <c>__</c> önekli).
/// </summary>
public class DmResource
{
    [JsonPropertyName("__dataId")]
    public string? __dataId { get; set; }

    public string? type { get; set; }
    public string? parentId { get; set; }
    public List<string>? ancestorIds { get; set; }
    public string? name { get; set; }
    public string? title { get; set; }
    public string? description { get; set; }
    public List<string>? tags { get; set; }

    /// <summary>Markdown içeriği (yalnızca <c>type=markdown</c>). Dosyalarda boştur.</summary>
    public string? content { get; set; }

    public string? contentType { get; set; }
    public string? mimeType { get; set; }
    public string? extension { get; set; }
    public long? size { get; set; }

    /// <summary>DG <c>file</c> alanı (yüklenen dosya metadata'sı: filePath, file_name, ...).</summary>
    public JsonElement? file { get; set; }

    public int? currentVersionNumber { get; set; }

    /// <summary>DG audit izi. <c>showHistory=true</c> ile döner; ilk <c>create</c> ve son <c>update</c> kaydından oluşturan/güncelleyen türetilir.</summary>
    [JsonPropertyName("__history")]
    public List<DmHistoryEntry>? __history { get; set; }
}

/// <summary>
/// DG veri kayıtlarının <c>__history</c> dizisindeki tek bir audit girdisi.
/// (Bu DG örneğinde audit <c>__createInfo</c>/<c>__lastUpdateInfo</c> yerine <c>__history</c>'de tutulur.)
/// </summary>
public class DmHistoryEntry
{
    /// <summary>İşlem türü: <c>create</c> / <c>update</c> / <c>delete</c>.</summary>
    public string? operation { get; set; }
    public string? userId { get; set; }
    /// <summary>İşlemi yapan kullanıcının görünen adı (alan adı <c>userEmail</c> olsa da ad bilgisini taşır).</summary>
    public string? userEmail { get; set; }
    public DateTime? timestamp { get; set; }
    public string? ipAddress { get; set; }

    /// <summary>Güncelleme işlemlerinde değişen alanlar (ör. <c>currentVersionNumber</c> → sürüm eşleştirme için).</summary>
    public Dictionary<string, JsonElement>? changes { get; set; }
}

/// <summary>
/// <c>dm_resource_versions</c> DG kaydının tip karşılığı (okuma). Her markdown kaydında bir anlık kopya.
/// </summary>
public class DmResourceVersion
{
    [JsonPropertyName("__dataId")]
    public string? __dataId { get; set; }

    public string? resourceId { get; set; }
    public int? versionNumber { get; set; }
    public string? changeNote { get; set; }
    public string? contentSnapshot { get; set; }
    public long? size { get; set; }
    public string? mimeType { get; set; }

    /// <summary>Sürümü yazan kullanıcı (yazımda açıkça gömülür; DG logging kapalı olduğu için).</summary>
    public string? createdBy { get; set; }

    /// <summary>Sürümün yazılma zamanı (UTC, yazımda açıkça gömülür).</summary>
    public DateTime? createdAt { get; set; }

    [JsonPropertyName("__history")]
    public List<DmHistoryEntry>? __history { get; set; }
}
