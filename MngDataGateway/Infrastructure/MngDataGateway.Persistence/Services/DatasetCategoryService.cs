using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.DatasetCategory;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// Dataset Category Service Implementation
/// @dataset_categories collection için CRUD operasyonları
/// </summary>
public class DatasetCategoryService : IDatasetCategoryService
{
    private readonly IMongoContextService _mongoContext;
    private readonly IUserInfoService _userInfoService;
    private readonly MngDataGatewaySettings _settings;
    private const string CollectionName = "@dataset_categories";
    private const string DeletedDataCollectionName = "__deletedDatas";

    public DatasetCategoryService(
        IMongoContextService mongoContext,
        IUserInfoService userInfoService,
        IOptions<MngDataGatewaySettings> settings)
    {
        _mongoContext = mongoContext ?? throw new ArgumentNullException(nameof(mongoContext));
        _userInfoService = userInfoService ?? throw new ArgumentNullException(nameof(userInfoService));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    private IMongoCollection<DatasetCategory> GetCollection()
    {
        var db = _mongoContext.GetDatabase();
        return db.GetCollection<DatasetCategory>(CollectionName);
    }

    private IMongoCollection<BsonDocument> GetDeletedDataCollection()
    {
        var db = _mongoContext.GetDatabase();
        return db.GetCollection<BsonDocument>(DeletedDataCollectionName);
    }

    /// <summary>
    /// Yeni kategori oluşturur
    /// </summary>
    public async Task<DatasetCategoryResponseDto> CreateAsync(CreateDatasetCategoryDto dto)
    {
        var collection = GetCollection();
        var userInfo = _userInfoService.GetCurrentUserInfo();
        var now = DateTime.UtcNow;

        // Duplicate check
        var exists = await collection.Find(x => x.categoryName == dto.CategoryName).AnyAsync();
        if (exists)
        {
            throw new InvalidOperationException($"'{dto.CategoryName}' adında bir kategori zaten mevcut");
        }

        // Create entity
        var entity = new DatasetCategory
        {
            __dataId = Guid.NewGuid().ToString(),
            categoryName = dto.CategoryName,
            categoryDescription = dto.CategoryDescription,

            __createInfo = new CreateInfo
            {
                createdAt = now,
                userInfo = userInfo
            },

            __history = new List<HistoryEntry>
            {
                new()
                {
                    operation = "insert",
                    timestamp = now,
                    userInfo = userInfo
                }
            }
        };

        await collection.InsertOneAsync(entity);

        return MapToDto(entity);
    }

    /// <summary>
    /// Kategorileri sayfalı olarak listeler
    /// </summary>
    public async Task<PagedResultDto<DatasetCategoryResponseDto>> GetAllAsync(int pageNumber = 1, int pageSize = 20, string? search = null)
    {
        var collection = GetCollection();

        // Validate pagination
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var skip = (pageNumber - 1) * pageSize;

        // Build filter for search
        var filterBuilder = Builders<DatasetCategory>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            // Search in categoryName and categoryDescription
            filter = filterBuilder.Or(
                filterBuilder.Regex(x => x.categoryName, new MongoDB.Bson.BsonRegularExpression(searchLower, "i")),
                filterBuilder.Regex(x => x.categoryDescription, new MongoDB.Bson.BsonRegularExpression(searchLower, "i"))
            );
        }

        // Get total count
        var totalCount = await collection.CountDocumentsAsync(filter);

        // Get items
        var items = await collection
            .Find(filter)
            .Sort(Builders<DatasetCategory>.Sort.Descending(x => x.__createInfo.createdAt))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return new PagedResultDto<DatasetCategoryResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// ID'ye göre kategori getirir
    /// </summary>
    public async Task<DatasetCategoryResponseDto?> GetByIdAsync(string dataId)
    {
        var collection = GetCollection();
        var entity = await collection.Find(x => x.__dataId == dataId).FirstOrDefaultAsync();

        return entity != null ? MapToDto(entity) : null;
    }

    /// <summary>
    /// Kategori günceller (sadece değişen alanları history'ye ekler)
    /// </summary>
    public async Task<DatasetCategoryResponseDto> UpdateAsync(string dataId, UpdateDatasetCategoryDto dto)
    {
        var collection = GetCollection();
        var entity = await collection.Find(x => x.__dataId == dataId).FirstOrDefaultAsync();

        if (entity == null)
        {
            throw new InvalidOperationException($"'{dataId}' ID'li kategori bulunamadı");
        }

        var userInfo = _userInfoService.GetCurrentUserInfo();
        var now = DateTime.UtcNow;
        var changes = new Dictionary<string, ChangeDetail>();

        // Detect changes
        if (dto.CategoryName != null && dto.CategoryName != entity.categoryName)
        {
            // Check duplicate
            var exists = await collection.Find(x => x.categoryName == dto.CategoryName && x.__dataId != dataId).AnyAsync();
            if (exists)
            {
                throw new InvalidOperationException($"'{dto.CategoryName}' adında bir kategori zaten mevcut");
            }

            changes["categoryName"] = new ChangeDetail
            {
                oldValue = entity.categoryName,
                newValue = dto.CategoryName
            };
            entity.categoryName = dto.CategoryName;
        }

        if (dto.CategoryDescription != null && dto.CategoryDescription != entity.categoryDescription)
        {
            changes["categoryDescription"] = new ChangeDetail
            {
                oldValue = entity.categoryDescription,
                newValue = dto.CategoryDescription
            };
            entity.categoryDescription = dto.CategoryDescription;
        }

        // Eğer değişiklik yoksa
        if (changes.Count == 0)
        {
            return MapToDto(entity);
        }

        // Update metadata
        entity.__lastUpdateInfo = new UpdateInfo
        {
            updatedAt = now,
            userInfo = userInfo
        };

        // Add history entry
        entity.__history.Add(new HistoryEntry
        {
            operation = "update",
            timestamp = now,
            userInfo = userInfo,
            changes = changes
        });

        // Limit history size
        var maxHistory = _settings.History?.MaxHistoryEntries ?? 50;
        while (entity.__history.Count > maxHistory)
        {
            entity.__history.RemoveAt(0);
        }

        // Save
        await collection.ReplaceOneAsync(x => x.__dataId == dataId, entity);

        return MapToDto(entity);
    }

    /// <summary>
    /// Kategoriyi siler (hard delete + __deletedDatas backup)
    /// </summary>
    public async Task<bool> DeleteAsync(string dataId)
    {
        var collection = GetCollection();
        var deletedCollection = GetDeletedDataCollection();

        var entity = await collection.Find(x => x.__dataId == dataId).FirstOrDefaultAsync();
        if (entity == null)
        {
            return false;
        }

        var userInfo = _userInfoService.GetCurrentUserInfo();
        var now = DateTime.UtcNow;
        var retentionDays = _settings.DeletedData?.RetentionDays ?? 7;

        // Add to __deletedDatas with TTL
        var deletedData = new BsonDocument
        {
            ["originalCollection"] = CollectionName,
            ["deletedData"] = entity.ToBsonDocument(),
            ["deletionInfo"] = new BsonDocument
            {
                ["timeISO"] = now,
                ["userInfo"] = userInfo.ToBsonDocument()
            },
            ["expireAt"] = now.AddDays(retentionDays)  // TTL
        };

        await deletedCollection.InsertOneAsync(deletedData);

        // Hard delete from main collection
        await collection.DeleteOneAsync(x => x.__dataId == dataId);

        return true;
    }

    /// <summary>
    /// Silinen kategoriyi geri yükler (__deletedDatas'dan)
    /// </summary>
    public async Task<DatasetCategoryResponseDto> RestoreAsync(string dataId)
    {
        var collection = GetCollection();
        var deletedCollection = GetDeletedDataCollection();

        // Find in __deletedDatas
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("originalCollection", CollectionName),
            Builders<BsonDocument>.Filter.Eq("deletedData.__dataId", dataId)
        );

        var deletedDoc = await deletedCollection.Find(filter).FirstOrDefaultAsync();
        if (deletedDoc == null)
        {
            throw new InvalidOperationException($"'{dataId}' ID'li silinmiş kategori bulunamadı");
        }

        // Restore entity
        var entityBson = deletedDoc["deletedData"].AsBsonDocument;
        var entity = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<DatasetCategory>(entityBson);

        // Check if already exists
        var exists = await collection.Find(x => x.__dataId == dataId).AnyAsync();
        if (exists)
        {
            throw new InvalidOperationException($"'{dataId}' ID'li kategori zaten mevcut");
        }

        // Add restore history entry
        var userInfo = _userInfoService.GetCurrentUserInfo();
        var now = DateTime.UtcNow;

        entity.__history.Add(new HistoryEntry
        {
            operation = "restore",
            timestamp = now,
            userInfo = userInfo
        });

        // Limit history size
        var maxHistory = _settings.History?.MaxHistoryEntries ?? 50;
        while (entity.__history.Count > maxHistory)
        {
            entity.__history.RemoveAt(0);
        }

        // Insert back to main collection
        await collection.InsertOneAsync(entity);

        // Remove from __deletedDatas
        await deletedCollection.DeleteOneAsync(filter);

        return MapToDto(entity);
    }

    /// <summary>
    /// Entity to DTO mapper
    /// </summary>
    private static DatasetCategoryResponseDto MapToDto(DatasetCategory entity)
    {
        return new DatasetCategoryResponseDto
        {
            DataId = entity.__dataId,
            CategoryName = entity.categoryName,
            CategoryDescription = entity.categoryDescription,
            CreateInfo = entity.__createInfo,
            LastUpdateInfo = entity.__lastUpdateInfo,
            HistoryCount = entity.__history.Count
        };
    }
}

