using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Dataset;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// Dataset Service Implementation
/// @datasets collection için CRUD operasyonları
/// </summary>
public class DatasetService : IDatasetService
{
    private readonly IMongoContextService _mongoContext;
    private readonly IUserInfoService _userInfoService;
    private readonly MngDataGatewaySettings _settings;
    private const string CollectionName = "@datasets";
    private const string DeletedDataCollectionName = "__deletedDatas";

    public DatasetService(
        IMongoContextService mongoContext,
        IUserInfoService userInfoService,
        IOptions<MngDataGatewaySettings> settings)
    {
        _mongoContext = mongoContext ?? throw new ArgumentNullException(nameof(mongoContext));
        _userInfoService = userInfoService ?? throw new ArgumentNullException(nameof(userInfoService));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    private IMongoCollection<DatasetSchema> GetCollection()
    {
        var db = _mongoContext.GetDatabase();
        return db.GetCollection<DatasetSchema>(CollectionName);
    }

    private IMongoCollection<BsonDocument> GetDeletedDataCollection()
    {
        var db = _mongoContext.GetDatabase();
        return db.GetCollection<BsonDocument>(DeletedDataCollectionName);
    }

    /// <summary>
    /// Yeni dataset schema oluşturur
    /// </summary>
    public async Task<DatasetResponseDto> CreateAsync(CreateDatasetDto dto)
    {
        var collection = GetCollection();
        var userInfo = _userInfoService.GetCurrentUserInfo();
        var now = DateTime.UtcNow;

        // Duplicate check (by name)
        var exists = await collection.Find(x => x.name == dto.Name).AnyAsync();
        if (exists)
        {
            throw new InvalidOperationException($"'{dto.Name}' adında bir dataset zaten mevcut");
        }

        // Create entity
        var entity = new DatasetSchema
        {
            __dataId = Guid.NewGuid().ToString(),
            name = dto.Name,
            description = dto.Description,
            category = dto.Category,
            forceSchema = dto.ForceSchema,
            logging = dto.Logging,
            publish_mode = dto.PublishMode,
            fields = ConvertFieldDefinitions(dto.Fields),
            validations = dto.Validations ?? new(),
            queries = dto.Queries ?? new(),
            indexList = dto.IndexList ?? new(),

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

        // Validate field definitions
        ValidateFieldDefinitions(entity.fields);

        // Validate incremental fields
        ValidateIncrementalFields(entity.fields);

        await collection.InsertOneAsync(entity);

        return MapToDto(entity);
    }

    /// <summary>
    /// Dataset schema'larını sayfalı olarak listeler
    /// </summary>
    public async Task<PagedResultDto<DatasetResponseDto>> GetAllAsync(int pageNumber = 1, int pageSize = 20)
    {
        var collection = GetCollection();

        // Validate pagination
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var skip = (pageNumber - 1) * pageSize;

        // Get total count
        var totalCount = await collection.CountDocumentsAsync(new BsonDocument());

        // Get items
        var items = await collection
            .Find(new BsonDocument())
            .Sort(Builders<DatasetSchema>.Sort.Descending(x => x.__createInfo.createdAt))
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return new PagedResultDto<DatasetResponseDto>
        {
            Items = items.Select(x => MapToDto(x)).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Name'e göre dataset schema getirir
    /// </summary>
    public async Task<DatasetResponseDto?> GetByNameAsync(string name)
    {
        var collection = GetCollection();
        var entity = await collection.Find(x => x.name == name).FirstOrDefaultAsync();

        return entity != null ? MapToDto(entity, includeDetails: true) : null;
    }

    /// <summary>
    /// Dataset schema günceller
    /// </summary>
    public async Task<DatasetResponseDto> UpdateAsync(string name, UpdateDatasetDto dto)
    {
        var collection = GetCollection();
        var entity = await collection.Find(x => x.name == name).FirstOrDefaultAsync();

        if (entity == null)
        {
            throw new InvalidOperationException($"'{name}' adlı dataset bulunamadı");
        }

        var userInfo = _userInfoService.GetCurrentUserInfo();
        var now = DateTime.UtcNow;
        var changes = new Dictionary<string, ChangeDetail>();

        // Detect changes
        if (dto.Description != null && dto.Description != entity.description)
        {
            changes["description"] = new ChangeDetail { oldValue = entity.description, newValue = dto.Description };
            entity.description = dto.Description;
        }

        if (dto.Category != null && dto.Category != entity.category)
        {
            changes["category"] = new ChangeDetail { oldValue = entity.category, newValue = dto.Category };
            entity.category = dto.Category;
        }

        if (dto.ForceSchema.HasValue && dto.ForceSchema.Value != entity.forceSchema)
        {
            changes["forceSchema"] = new ChangeDetail { oldValue = entity.forceSchema, newValue = dto.ForceSchema.Value };
            entity.forceSchema = dto.ForceSchema.Value;
        }

        if (dto.Logging != null && dto.Logging != entity.logging)
        {
            changes["logging"] = new ChangeDetail { oldValue = entity.logging, newValue = dto.Logging };
            entity.logging = dto.Logging;
        }

        if (dto.PublishMode != null && dto.PublishMode != entity.publish_mode)
        {
            changes["publish_mode"] = new ChangeDetail { oldValue = entity.publish_mode, newValue = dto.PublishMode };
            entity.publish_mode = dto.PublishMode;
        }

        // Array updates (replace entire array if provided)
        if (dto.Fields != null)
        {
            var convertedFields = ConvertFieldDefinitions(dto.Fields);
            ValidateFieldDefinitions(convertedFields);
            ValidateIncrementalFields(convertedFields);
            changes["fields"] = new ChangeDetail { oldValue = $"{entity.fields.Count} fields", newValue = $"{convertedFields.Count} fields" };
            entity.fields = convertedFields;
        }

        if (dto.Validations != null)
        {
            changes["validations"] = new ChangeDetail { oldValue = $"{entity.validations.Count} validations", newValue = $"{dto.Validations.Count} validations" };
            entity.validations = dto.Validations;
        }

        if (dto.Queries != null)
        {
            // Convert queries, handling JsonElement in pipeline
            var convertedQueries = dto.Queries.Select(q => new QueryDefinition
            {
                name = q.name,
                description = q.description,
                parameters = q.parameters,
                pipeline = q.pipeline != null ? ConvertPipelineToObjectList(q.pipeline) : null
            }).ToList();
            
            changes["queries"] = new ChangeDetail { oldValue = $"{entity.queries.Count} queries", newValue = $"{convertedQueries.Count} queries" };
            entity.queries = convertedQueries;
        }

        if (dto.IndexList != null)
        {
            // Add new indexes (keep old ones)
            var newIndexes = dto.IndexList.Where(ni => !entity.indexList.Any(ei => ei.name == ni.name)).ToList();
            if (newIndexes.Any())
            {
                changes["indexList"] = new ChangeDetail 
                { 
                    oldValue = $"{entity.indexList.Count} indexes", 
                    newValue = $"{entity.indexList.Count + newIndexes.Count} indexes (added {newIndexes.Count})" 
                };
                entity.indexList.AddRange(newIndexes);
            }
        }

        // Eğer değişiklik yoksa
        if (changes.Count == 0)
        {
            return MapToDto(entity, includeDetails: true);
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
        await collection.ReplaceOneAsync(x => x.name == name, entity);

        return MapToDto(entity, includeDetails: true);
    }

    /// <summary>
    /// Dataset schema'yı siler (hard delete + __deletedDatas backup)
    /// </summary>
    public async Task<bool> DeleteAsync(string name)
    {
        var collection = GetCollection();
        var deletedCollection = GetDeletedDataCollection();

        var entity = await collection.Find(x => x.name == name).FirstOrDefaultAsync();
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
            ["expireAt"] = now.AddDays(retentionDays)
        };

        await deletedCollection.InsertOneAsync(deletedData);

        // Hard delete from main collection
        await collection.DeleteOneAsync(x => x.name == name);

        // NOTE: Collection (@tasks, @users, etc.) is NOT deleted!
        // Data remains intact. Only schema metadata is removed.

        return true;
    }

    /// <summary>
    /// Silinen dataset schema'yı geri yükler
    /// </summary>
    public async Task<DatasetResponseDto> RestoreAsync(string name)
    {
        var collection = GetCollection();
        var deletedCollection = GetDeletedDataCollection();

        // Find in __deletedDatas
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("originalCollection", CollectionName),
            Builders<BsonDocument>.Filter.Eq("deletedData.name", name)
        );

        var deletedDoc = await deletedCollection.Find(filter).FirstOrDefaultAsync();
        if (deletedDoc == null)
        {
            throw new InvalidOperationException($"'{name}' adlı silinmiş dataset bulunamadı");
        }

        // Restore entity
        var entityBson = deletedDoc["deletedData"].AsBsonDocument;
        var entity = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<DatasetSchema>(entityBson);

        // Check if already exists
        var exists = await collection.Find(x => x.name == name).AnyAsync();
        if (exists)
        {
            throw new InvalidOperationException($"'{name}' adlı dataset zaten mevcut");
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

        return MapToDto(entity, includeDetails: true);
    }

    /// <summary>
    /// Convert FieldDefinitions from DTO (object defaultValue) to Entity (BsonValue)
    /// </summary>
    private List<FieldDefinition> ConvertFieldDefinitions(List<FieldDefinition>? dtoFields)
    {
        if (dtoFields == null || !dtoFields.Any())
            return new List<FieldDefinition>();

        var result = new List<FieldDefinition>();

        foreach (var field in dtoFields)
        {
            var converted = new FieldDefinition
            {
                fieldType = field.fieldType,
                name = field.name,
                title = field.title,
                mandatory = field.mandatory,
                unique = field.unique,
                isArray = field.isArray,
                relationDataset = field.relationDataset,
                incrementalOptions = field.incrementalOptions
            };

            // Convert defaultValue to BsonValue if present
            if (field.defaultValue != null)
            {
                try
                {
                    converted.defaultValue = MongoDB.Bson.BsonValue.Create(field.defaultValue);
                }
                catch
                {
                    // If conversion fails, try as string
                    converted.defaultValue = MongoDB.Bson.BsonValue.Create(field.defaultValue.ToString());
                }
            }

            result.Add(converted);
        }

        return result;
    }

    /// <summary>
    /// Field definitions validation
    /// </summary>
    private void ValidateFieldDefinitions(List<FieldDefinition> fields)
    {
        var fieldNames = new HashSet<string>();

        foreach (var field in fields)
        {
            // Check duplicate field names
            if (!fieldNames.Add(field.name))
            {
                throw new InvalidOperationException($"Duplicate field name: '{field.name}'");
            }

            // Validate field type
            var validTypes = new[] { "text", "number", "bool", "datetime", "object", "relation", "persons", "personGroups", "incremental" };
            if (!validTypes.Contains(field.fieldType))
            {
                throw new InvalidOperationException($"Invalid field type: '{field.fieldType}' in field '{field.name}'");
            }

            // Relation field must have relationDataset
            if (field.fieldType == "relation" && string.IsNullOrWhiteSpace(field.relationDataset))
            {
                throw new InvalidOperationException($"Relation field '{field.name}' must specify relationDataset");
            }

            // Incremental field must have incrementalOptions
            if (field.fieldType == "incremental" && field.incrementalOptions == null)
            {
                throw new InvalidOperationException($"Incremental field '{field.name}' must have incrementalOptions");
            }
        }
    }

    /// <summary>
    /// Incremental fields validation
    /// </summary>
    private void ValidateIncrementalFields(List<FieldDefinition> fields)
    {
        var incrementalFields = fields.Where(f => f.fieldType == "incremental").ToList();

        foreach (var field in incrementalFields)
        {
            // Incremental fields must be unique
            if (!field.unique)
            {
                throw new InvalidOperationException($"Incremental field '{field.name}' must be unique");
            }

            // Incremental fields must be mandatory
            if (!field.mandatory)
            {
                throw new InvalidOperationException($"Incremental field '{field.name}' must be mandatory");
            }

            // Cannot be array
            if (field.isArray)
            {
                throw new InvalidOperationException($"Incremental field '{field.name}' cannot be an array");
            }
        }
    }

    /// <summary>
    /// Entity to DTO mapper
    /// </summary>
    private static DatasetResponseDto MapToDto(DatasetSchema entity, bool includeDetails = false)
    {
        return new DatasetResponseDto
        {
            DataId = entity.__dataId,
            Name = entity.name,
            Description = entity.description,
            Category = entity.category,
            ForceSchema = entity.forceSchema,
            Logging = entity.logging,
            PublishMode = entity.publish_mode,
            FieldsCount = entity.fields.Count,
            Fields = includeDetails ? entity.fields : null,
            ValidationsCount = entity.validations.Count,
            QueriesCount = entity.queries.Count,
            IndexListCount = entity.indexList.Count,
            CreateInfo = entity.__createInfo,
            LastUpdateInfo = entity.__lastUpdateInfo,
            HistoryCount = entity.__history.Count
        };
    }

    /// <summary>
    /// Name'e göre dataset schema entity getirir (internal use for data operations)
    /// </summary>
    public async Task<DatasetSchema?> GetSchemaEntityByNameAsync(string name)
    {
        var collection = GetCollection();
        var filter = Builders<DatasetSchema>.Filter.Eq(x => x.name, name);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Convert pipeline from List<object> (which may contain JsonElement) to List<object> with proper types
    /// Recursively converts JsonElement to proper objects
    /// </summary>
    private List<object>? ConvertPipelineToObjectList(List<object>? pipeline)
    {
        if (pipeline == null)
            return null;

        var result = new List<object>();
        foreach (var item in pipeline)
        {
            result.Add(ConvertJsonElementToObject(item));
        }
        return result;
    }

    /// <summary>
    /// Recursively convert JsonElement to object
    /// </summary>
    private object ConvertJsonElementToObject(object? value)
    {
        if (value == null)
            return new object();

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Object => ConvertJsonObjectToDictionary(jsonElement),
                JsonValueKind.Array => jsonElement.EnumerateArray().Select(e => ConvertJsonElementToObject(e)).ToList(),
                JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                JsonValueKind.Number => jsonElement.TryGetInt64(out var longVal) ? longVal : jsonElement.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => (object?)null,
                _ => jsonElement.GetRawText()
            };
        }

        if (value is System.Collections.IEnumerable enumerable && !(value is string))
        {
            return enumerable.Cast<object>().Select(ConvertJsonElementToObject).ToList();
        }

        return value;
    }

    /// <summary>
    /// Convert JsonElement object to Dictionary<string, object>
    /// </summary>
    private Dictionary<string, object> ConvertJsonObjectToDictionary(JsonElement jsonElement)
    {
        var dict = new Dictionary<string, object>();
        foreach (var prop in jsonElement.EnumerateObject())
        {
            dict[prop.Name] = ConvertJsonElementToObject(prop.Value);
        }
        return dict;
    }
}

