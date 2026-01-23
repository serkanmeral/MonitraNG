using MngDataGateway.Domain.Entities.Base;
using MongoDB.Bson.Serialization.Attributes;

namespace MngDataGateway.Domain.Entities;

/// <summary>
/// Dataset Category Entity - @dataset_categories collection
/// Kategori bazlı dataset organizasyonu için
/// </summary>
[BsonIgnoreExtraElements]
public class DatasetCategory : BaseEntity
{
    /// <summary>
    /// Kategori adı (unique)
    /// </summary>
    [BsonElement("name")]
    public string categoryName { get; set; } = string.Empty;

    /// <summary>
    /// Kategori açıklaması
    /// </summary>
    [BsonElement("description")]
    public string? categoryDescription { get; set; }

    /// <summary>
    /// Sistem kategorisi mi? (Sistem datasetlerinin içinde bulunacağı kategori)
    /// </summary>
    [BsonElement("isSystemCategory")]
    public bool isSystemCategory { get; set; } = false;
}

