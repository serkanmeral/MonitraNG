using MngDocument.Application.Contracts.Generation;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DocumentContextRelationDefinition
{
    public string Path { get; init; } = string.Empty;
    public string Dataset { get; init; } = string.Empty;
    public bool Optional { get; init; }
}

public sealed class DocumentContextTypeDefinition
{
    public string Type { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string RootDataset { get; init; } = string.Empty;
    public IReadOnlyList<DocumentContextRelationDefinition> Relations { get; init; }
        = Array.Empty<DocumentContextRelationDefinition>();
    public IReadOnlyList<DocumentContextFieldDto> Fields { get; init; }
        = Array.Empty<DocumentContextFieldDto>();
}

public static class DocumentContextCatalog
{
    private static readonly IReadOnlyDictionary<string, DocumentContextTypeDefinition> Types =
        new Dictionary<string, DocumentContextTypeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["odak.siparis.line"] = new DocumentContextTypeDefinition
            {
                Type = "odak.siparis.line",
                DisplayName = "Sipariş kalemi",
                RootDataset = "odak_siparis_kalemleri",
                Relations = new[]
                {
                    new DocumentContextRelationDefinition
                    {
                        Path = "parentPackageId",
                        Dataset = "odak_is_paketleri"
                    },
                    new DocumentContextRelationDefinition
                    {
                        Path = "parentPackageId.customerId",
                        Dataset = "odak_musteriler",
                        Optional = true
                    },
                    new DocumentContextRelationDefinition
                    {
                        Path = "productId",
                        Dataset = "odak_urunler",
                        Optional = true
                    }
                },
                Fields = new[]
                {
                    Field("lineNo", "Kalem no", "number"),
                    Field("customerPoNo", "Müşteri PO no"),
                    Field("customerPoItemNo", "Müşteri PO kalem no", "number"),
                    Field("customerProjectNo", "Müşteri proje no"),
                    Field("customerJobNo", "Müşteri iş emri"),
                    Field("description", "Parça tanımı"),
                    Field("poItemRevNo", "PO kalem revizyon"),
                    Field("quantity", "Miktar", "number"),
                    Field("unit", "Birim"),
                    Field("shippedQuantity", "Sevk miktarı", "number"),
                    Field("deliveryDate", "Kalem termin", "date"),
                    Field("shipmentDate", "Sevkiyat tarihi", "date"),
                    Field("shipmentAddress", "Sevkiyat adresi"),
                    Field("qualityReqs", "Kalite isterleri (not)"),
                    Field("isFai", "FAI gerekli", "bool"),
                    Field("isFaiComplete", "FAI tamamlandı", "bool"),
                    Field("cocDocNo", "Uygunluk belgesi no"),
                    Field("cocGeneratedAt", "CoC üretim tarihi", "date"),
                    Field("cocTemplateName", "CoC şablon adı"),
                    Field("parentPackageId.packageNo", "İş paketi no"),
                    Field("parentPackageId.name", "İş paketi adı"),
                    Field("parentPackageId.status", "İş paketi durumu"),
                    Field("parentPackageId.beginDate", "İş paketi başlangıç", "date"),
                    Field("parentPackageId.deliveryDate", "İş paketi termin", "date"),
                    Field("parentPackageId.deliveryAddress", "Teslimat adresi"),
                    Field("parentPackageId.customerId.unvan", "Müşteri unvan"),
                    Field("productId.partNumber", "Teknik resim no"),
                    Field("productId.revizyon", "Ürün revizyon")
                }
            },
            ["odak.siparis.package"] = new DocumentContextTypeDefinition
            {
                Type = "odak.siparis.package",
                DisplayName = "İş paketi",
                RootDataset = "odak_is_paketleri",
                Relations = new[]
                {
                    new DocumentContextRelationDefinition
                    {
                        Path = "customerId",
                        Dataset = "odak_musteriler",
                        Optional = true
                    }
                },
                Fields = new[]
                {
                    Field("packageNo", "İş paketi no"),
                    Field("name", "İş paketi adı"),
                    Field("status", "Durum"),
                    Field("beginDate", "Başlangıç", "date"),
                    Field("deliveryDate", "Termin", "date"),
                    Field("deliveryAddress", "Teslimat adresi"),
                    Field("lineCount", "Kalem sayısı", "number"),
                    Field("partCount", "Parça sayısı", "number"),
                    Field("stockCount", "Stok sayısı", "number"),
                    Field("shippedCount", "Sevk sayısı", "number"),
                    Field("customerId.unvan", "Müşteri unvan"),
                    Field("notes", "Notlar")
                }
            }
        };

    public static IReadOnlyList<DocumentContextTypeDefinition> All() => Types.Values.ToList();

    public static DocumentContextTypeDefinition? TryGet(string? type) =>
        string.IsNullOrWhiteSpace(type) ? null
        : Types.TryGetValue(type.Trim(), out var def) ? def : null;

    public static DocumentContextTypeDefinition GetRequired(string type) =>
        TryGet(type) ?? throw new InvalidOperationException($"Unknown document context type: {type}");

    private static DocumentContextFieldDto Field(string path, string label, string dataType = "text") =>
        new() { Path = path, Label = label, DataType = dataType };
}
