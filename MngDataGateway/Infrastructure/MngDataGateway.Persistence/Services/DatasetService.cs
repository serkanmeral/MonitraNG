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

        // Process permissions: if provided but all permission types are null/empty, set to null
        PermissionsDefinition? processedPermissions = null;
        if (dto.Permissions != null)
        {
            // Check if any permission type has groups defined
            var hasAnyPermissions = (dto.Permissions.read?.groups != null && dto.Permissions.read.groups.Count > 0) ||
                                   (dto.Permissions.create?.groups != null && dto.Permissions.create.groups.Count > 0) ||
                                   (dto.Permissions.update?.groups != null && dto.Permissions.update.groups.Count > 0) ||
                                   (dto.Permissions.delete?.groups != null && dto.Permissions.delete.groups.Count > 0);
            
            if (hasAnyPermissions)
            {
                processedPermissions = dto.Permissions;
            }
            // else: processedPermissions remains null (will be ignored by BsonIgnoreIfNull)
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
            queries = dto.Queries != null ? ConvertQueryDefinitions(dto.Queries) : new(),
            indexList = dto.IndexList ?? new(),
            permissions = processedPermissions,

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
            var convertedQueries = ConvertQueryDefinitions(dto.Queries);
            
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

        // Permissions update (replace entire permissions if provided)
        // Check if permissions object is provided (even if all permission types are null)
        // We need to check if the property was explicitly set in the DTO
        // If all permission types are null/empty, set permissions to null (will be ignored by BsonIgnoreIfNull)
        if (dto.Permissions != null)
        {
            var oldPermissionsSummary = entity.permissions != null 
                ? $"read:{entity.permissions.read?.groups?.Count ?? 0}, create:{entity.permissions.create?.groups?.Count ?? 0}, update:{entity.permissions.update?.groups?.Count ?? 0}, delete:{entity.permissions.delete?.groups?.Count ?? 0}"
                : "none";
            
            // Check if any permission type has groups defined
            var hasAnyPermissions = (dto.Permissions.read?.groups != null && dto.Permissions.read.groups.Count > 0) ||
                                   (dto.Permissions.create?.groups != null && dto.Permissions.create.groups.Count > 0) ||
                                   (dto.Permissions.update?.groups != null && dto.Permissions.update.groups.Count > 0) ||
                                   (dto.Permissions.delete?.groups != null && dto.Permissions.delete.groups.Count > 0);
            
            if (hasAnyPermissions)
            {
                // At least one permission type has groups, save the permissions object
                var newPermissionsSummary = $"read:{dto.Permissions.read?.groups?.Count ?? 0}, create:{dto.Permissions.create?.groups?.Count ?? 0}, update:{dto.Permissions.update?.groups?.Count ?? 0}, delete:{dto.Permissions.delete?.groups?.Count ?? 0}";
                changes["permissions"] = new ChangeDetail { oldValue = oldPermissionsSummary, newValue = newPermissionsSummary };
                entity.permissions = dto.Permissions;
            }
            else
            {
                // All permission types are null or empty, remove permissions (set to null)
                if (entity.permissions != null)
                {
                    changes["permissions"] = new ChangeDetail { oldValue = oldPermissionsSummary, newValue = "none" };
                    entity.permissions = null;
                }
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
                incrementalOptions = field.incrementalOptions,
                validation = field.validation // Copy validation rules
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
    /// Convert QueryDefinitionDto from DTO to Entity QueryDefinition, handling JsonElement in pipeline
    /// Supports both new format (List<QueryParameterDefinitionDto>) and legacy format (List<string>)
    /// </summary>
    private static List<QueryDefinition> ConvertQueryDefinitions(List<MngDataGateway.Application.DTOs.Dataset.QueryDefinitionDto>? queries)
    {
        if (queries == null || queries.Count == 0)
        {
            return new List<QueryDefinition>();
        }

        var result = new List<QueryDefinition>();

        foreach (var query in queries)
        {
            // Convert parameters: support both new format (List<QueryParameterDefinitionDto>) and legacy format (List<string>)
            object? parameters = null;
            if (query.Parameters != null)
            {
                // Check if it's a list of strings (legacy format)
                if (query.Parameters is System.Text.Json.JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var firstItem = jsonElement.EnumerateArray().FirstOrDefault();
                        if (firstItem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            // Legacy format: List<string>
                            var paramNames = new List<string>();
                            foreach (var item in jsonElement.EnumerateArray())
                            {
                                paramNames.Add(item.GetString() ?? string.Empty);
                            }
                            parameters = paramNames;
                        }
                        else if (firstItem.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            // New format: List<QueryParameterDefinitionDto>
                            var paramDefs = new List<QueryParameterDefinition>();
                            foreach (var item in jsonElement.EnumerateArray())
                            {
                                var name = item.GetProperty("name").GetString() ?? item.GetProperty("Name").GetString() ?? string.Empty;
                                var type = item.GetProperty("type").GetString() ?? item.GetProperty("Type").GetString() ?? "text";
                                var description = item.TryGetProperty("description", out var descProp) ? descProp.GetString() 
                                    : (item.TryGetProperty("Description", out var descProp2) ? descProp2.GetString() : null);
                                var required = item.TryGetProperty("required", out var reqProp) ? reqProp.GetBoolean() 
                                    : (item.TryGetProperty("Required", out var reqProp2) ? reqProp2.GetBoolean() : true);
                                
                                paramDefs.Add(new QueryParameterDefinition
                                {
                                    name = name,
                                    type = type,
                                    description = description,
                                    required = required
                                });
                            }
                            parameters = paramDefs;
                        }
                    }
                }
                else if (query.Parameters is List<string> stringList)
                {
                    // Legacy format: List<string>
                    parameters = stringList;
                }
                else if (query.Parameters is List<MngDataGateway.Application.DTOs.Dataset.QueryParameterDefinitionDto> paramDtoList)
                {
                    // New format: List<QueryParameterDefinitionDto>
                    var paramDefs = paramDtoList.Select(p => new QueryParameterDefinition
                    {
                        name = p.Name,
                        type = p.Type,
                        description = p.Description,
                        required = p.Required
                    }).ToList();
                    parameters = paramDefs;
                }
                else
                {
                    // Try to deserialize as one of the formats
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(query.Parameters);
                        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<object>>(json);
                        if (deserialized != null && deserialized.Count > 0)
                        {
                            if (deserialized[0] is string)
                            {
                                // Legacy format
                                parameters = deserialized.Cast<string>().ToList();
                            }
                            else
                            {
                                // Try new format
                                var paramDefs = System.Text.Json.JsonSerializer.Deserialize<List<MngDataGateway.Application.DTOs.Dataset.QueryParameterDefinitionDto>>(json);
                                if (paramDefs != null)
                                {
                                    parameters = paramDefs.Select(p => new QueryParameterDefinition
                                    {
                                        name = p.Name,
                                        type = p.Type,
                                        description = p.Description,
                                        required = p.Required
                                    }).ToList();
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If deserialization fails, keep as-is (will be stored as BsonValue)
                        parameters = query.Parameters;
                    }
                }
            }

            // Convert parameters to BsonArray for MongoDB serialization
            MongoDB.Bson.BsonArray? parametersBson = null;
            if (parameters != null)
            {
                if (parameters is List<string> stringList)
                {
                    // Legacy format: List<string> -> BsonArray of strings
                    parametersBson = new MongoDB.Bson.BsonArray(stringList.Select(s => new MongoDB.Bson.BsonString(s)));
                }
                else if (parameters is List<QueryParameterDefinition> paramDefList)
                {
                    // New format: List<QueryParameterDefinition> -> BsonArray of BsonDocuments
                    parametersBson = new MongoDB.Bson.BsonArray();
                    foreach (var paramDef in paramDefList)
                    {
                        var paramDoc = new MongoDB.Bson.BsonDocument
                        {
                            { "name", paramDef.name },
                            { "type", paramDef.type },
                            { "required", paramDef.required }
                        };
                        if (!string.IsNullOrEmpty(paramDef.description))
                        {
                            paramDoc["description"] = paramDef.description;
                        }
                        parametersBson.Add(paramDoc);
                    }
                }
            }

            var converted = new QueryDefinition
            {
                name = query.Name,
                description = query.Description,
                parameters = parametersBson, // Store as BsonArray for MongoDB compatibility
                pipeline = ConvertPipelineToBsonDocuments(query.Pipeline)
            };

            result.Add(converted);
        }

        return result;
    }

    /// <summary>
    /// Convert pipeline from List<object> (which may contain JsonElement) to List<BsonDocument>
    /// Preserves numeric types correctly (especially for $sort stage with -1 and 1 values)
    /// </summary>
    private static List<MongoDB.Bson.BsonDocument>? ConvertPipelineToBsonDocuments(List<object>? pipeline)
    {
        if (pipeline == null || pipeline.Count == 0)
        {
            return null;
        }

        var result = new List<MongoDB.Bson.BsonDocument>();

        foreach (var stage in pipeline)
        {
            MongoDB.Bson.BsonDocument bsonDoc;

            // Handle JsonElement (from JSON deserialization)
            if (stage is System.Text.Json.JsonElement jsonElement)
            {
                bsonDoc = ConvertJsonElementToBsonDocument(jsonElement);
            }
            // Handle BsonDocument (already converted)
            else if (stage is MongoDB.Bson.BsonDocument bsonDocument)
            {
                bsonDoc = bsonDocument;
            }
            // Handle Dictionary<string, object> (from model binding)
            else if (stage is Dictionary<string, object> dict)
            {
                bsonDoc = ConvertDictionaryToBsonDocument(dict);
            }
            // Try to serialize and parse
            else
            {
                var json = System.Text.Json.JsonSerializer.Serialize(stage);
                bsonDoc = MongoDB.Bson.BsonDocument.Parse(json);
            }

            result.Add(bsonDoc);
        }

        return result;
    }

    /// <summary>
    /// Convert JsonElement to BsonDocument, preserving numeric types correctly
    /// </summary>
    private static MongoDB.Bson.BsonDocument ConvertJsonElementToBsonDocument(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            throw new InvalidOperationException("Pipeline stage must be an object");
        }

        var bsonDoc = new MongoDB.Bson.BsonDocument();

        foreach (var prop in element.EnumerateObject())
        {
            bsonDoc[prop.Name] = ConvertJsonElementToBsonValue(prop.Value);
        }

        return bsonDoc;
    }

    /// <summary>
    /// Convert JsonElement to BsonValue, preserving numeric types correctly
    /// </summary>
    private static MongoDB.Bson.BsonValue ConvertJsonElementToBsonValue(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.String:
                return new MongoDB.Bson.BsonString(element.GetString() ?? string.Empty);

            case System.Text.Json.JsonValueKind.Number:
                // Try to preserve as integer if possible (important for $sort: { field: 1 } or { field: -1 })
                if (element.TryGetInt32(out var intVal))
                {
                    return new MongoDB.Bson.BsonInt32(intVal);
                }
                if (element.TryGetInt64(out var longVal))
                {
                    return new MongoDB.Bson.BsonInt64(longVal);
                }
                return new MongoDB.Bson.BsonDouble(element.GetDouble());

            case System.Text.Json.JsonValueKind.True:
                return MongoDB.Bson.BsonBoolean.True;

            case System.Text.Json.JsonValueKind.False:
                return MongoDB.Bson.BsonBoolean.False;

            case System.Text.Json.JsonValueKind.Null:
                return MongoDB.Bson.BsonNull.Value;

            case System.Text.Json.JsonValueKind.Object:
                var objDoc = new MongoDB.Bson.BsonDocument();
                foreach (var prop in element.EnumerateObject())
                {
                    objDoc[prop.Name] = ConvertJsonElementToBsonValue(prop.Value);
                }
                return objDoc;

            case System.Text.Json.JsonValueKind.Array:
                var array = new MongoDB.Bson.BsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(ConvertJsonElementToBsonValue(item));
                }
                return array;

            default:
                return new MongoDB.Bson.BsonString(element.GetRawText());
        }
    }

    /// <summary>
    /// Convert Dictionary to BsonDocument, preserving numeric types correctly
    /// </summary>
    private static MongoDB.Bson.BsonDocument ConvertDictionaryToBsonDocument(Dictionary<string, object> dict)
    {
        var bsonDoc = new MongoDB.Bson.BsonDocument();

        foreach (var kvp in dict)
        {
            bsonDoc[kvp.Key] = ConvertObjectToBsonValue(kvp.Value);
        }

        return bsonDoc;
    }

    /// <summary>
    /// Convert object to BsonValue, preserving numeric types correctly
    /// </summary>
    private static MongoDB.Bson.BsonValue ConvertObjectToBsonValue(object? value)
    {
        if (value == null)
            return MongoDB.Bson.BsonNull.Value;

        // Handle BsonValue types directly
        if (value is MongoDB.Bson.BsonValue bsonValue)
            return bsonValue;

        // Handle primitive types
        if (value is int intVal)
            return new MongoDB.Bson.BsonInt32(intVal);

        if (value is long longVal)
            return new MongoDB.Bson.BsonInt64(longVal);

        if (value is double doubleVal)
            return new MongoDB.Bson.BsonDouble(doubleVal);

        if (value is bool boolVal)
            return new MongoDB.Bson.BsonBoolean(boolVal);

        if (value is string strVal)
            return new MongoDB.Bson.BsonString(strVal);

        if (value is DateTime dateTimeVal)
            return new MongoDB.Bson.BsonDateTime(dateTimeVal);

        // Handle Dictionary
        if (value is Dictionary<string, object> dict)
            return ConvertDictionaryToBsonDocument(dict);

        // Handle List/Array
        if (value is System.Collections.IEnumerable enumerable && !(value is string))
        {
            var bsonArray = new MongoDB.Bson.BsonArray();
            foreach (var item in enumerable)
            {
                bsonArray.Add(ConvertObjectToBsonValue(item));
            }
            return bsonArray;
        }

        // Handle JsonElement
        if (value is System.Text.Json.JsonElement jsonElement)
            return ConvertJsonElementToBsonValue(jsonElement);

        // Fallback: serialize to JSON and parse
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            return MongoDB.Bson.BsonDocument.Parse(json);
        }
        catch
        {
            return new MongoDB.Bson.BsonString(value.ToString() ?? string.Empty);
        }
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
    /// Convert BsonDocument to object for JSON serialization
    /// </summary>
    private static object ConvertBsonDocumentToObject(MongoDB.Bson.BsonDocument doc)
    {
        // Convert BsonDocument to JSON string, then deserialize to object
        var json = doc.ToJson();
        return System.Text.Json.JsonSerializer.Deserialize<object>(json) ?? new object();
    }

    /// <summary>
    /// Convert QueryDefinition list for response (BsonDocument -> serializable format)
    /// </summary>
    private static List<MngDataGateway.Application.DTOs.Dataset.QueryDefinitionResponseDto>? ConvertQueriesForResponse(List<QueryDefinition>? queries)
    {
        if (queries == null || queries.Count == 0)
        {
            return null;
        }

        return queries.Select(q => new MngDataGateway.Application.DTOs.Dataset.QueryDefinitionResponseDto
        {
            Name = q.name,
            Description = q.description,
            Parameters = ConvertParametersForResponse(q.parameters),
            Pipeline = q.pipeline?.Select(doc => ConvertBsonDocumentToObject(doc)).ToList()
        }).ToList();
    }

    /// <summary>
    /// Convert parameters for response - supports both new format (List<QueryParameterDefinition>) and legacy format (List<string>)
    /// </summary>
    private static object? ConvertParametersForResponse(object? parameters)
    {
        if (parameters == null)
            return null;

        // Check if it's a list of QueryParameterDefinition (new format)
        if (parameters is List<QueryParameterDefinition> paramDefs)
        {
            return paramDefs.Select(p => new QueryParameterDefinitionResponseDto
            {
                Name = p.name,
                Type = p.type,
                Description = p.description,
                Required = p.required
            }).ToList();
        }

        // Check if it's a list of strings (legacy format)
        if (parameters is List<string> stringList)
        {
            return stringList;
        }

        // Try to deserialize from BsonValue
        try
        {
            if (parameters is BsonArray bsonArray)
            {
                var firstElement = bsonArray.FirstOrDefault();
                if (firstElement != null)
                {
                    if (firstElement is BsonString)
                    {
                        // Legacy format: List<string>
                        return bsonArray.Select(e => e.AsString).ToList();
                    }
                    else if (firstElement is BsonDocument)
                    {
                        // New format: List<QueryParameterDefinition>
                        return bsonArray.Select(e => 
                        {
                            var doc = e.AsBsonDocument;
                            return new QueryParameterDefinitionResponseDto
                            {
                                Name = doc.GetValue("name", "").AsString,
                                Type = doc.GetValue("type", "text").AsString,
                                Description = doc.Contains("description") ? doc["description"].AsString : null,
                                Required = doc.Contains("required") ? doc["required"].AsBoolean : true
                            };
                        }).ToList();
                    }
                }
            }
        }
        catch
        {
            // If conversion fails, return as-is
        }

        // Fallback: return as-is (will be serialized as JSON)
        return parameters;
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
            Validations = includeDetails ? entity.validations : null,
            QueriesCount = entity.queries.Count,
            Queries = includeDetails ? ConvertQueriesForResponse(entity.queries) : null,
            IndexListCount = entity.indexList.Count,
            IndexList = includeDetails ? entity.indexList : null,
            Permissions = entity.permissions,
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

