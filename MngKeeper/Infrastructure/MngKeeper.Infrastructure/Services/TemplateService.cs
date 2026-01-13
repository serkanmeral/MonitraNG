using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.DTOs;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Persistence.Extensions;
using System.Text;
using System.Text.Json;

namespace MngKeeper.Infrastructure.Services;

/// <summary>
/// Template service implementation
/// Manages template creation, storage in MinIO, and retrieval
/// </summary>
public class TemplateService : ITemplateService
{
    private const string SystemBucketName = "system";
    
    private readonly ITemplateRepository _templateRepository;
    private readonly IMinioService _minioService;
    private readonly IMongoClient _mongoClient;
    private readonly ILogger<TemplateService> _logger;
    private readonly MngKeeperSettings _settings;
    private readonly string _templatesFolderPath;

    public TemplateService(
        ITemplateRepository templateRepository,
        IMinioService minioService,
        IMongoClient mongoClient,
        IOptions<MngKeeperSettings> settings,
        ILogger<TemplateService> logger)
    {
        _templateRepository = templateRepository;
        _minioService = minioService;
        _mongoClient = mongoClient;
        _settings = settings.Value;
        _logger = logger;
        
        // Build templates folder path: {SystemFolderPath}/templates/
        // Default: "System/templates/" (for system/System/templates/ structure)
        var systemFolder = _settings.MinIO.SystemFolderPath ?? "System";
        _templatesFolderPath = $"{systemFolder}/templates/";
    }

    public async Task<Template> CreateTemplateAsync(
        string templateName,
        string description,
        string sourceDomainId,
        string sourceDatabaseName,
        List<Domain.Entities.SelectedCollection> collections,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        // Validate template name uniqueness
        var exists = await _templateRepository.ExistsByNameAsync(templateName);
        if (exists)
        {
            throw new InvalidOperationException($"Template with name '{templateName}' already exists");
        }

        // Ensure system bucket exists
        await EnsureSystemBucketExistsAsync(cancellationToken);

        // Read collections from source database and create template content
        var templateContent = await CreateTemplateContentAsync(
            templateName,
            sourceDatabaseName,
            collections,
            cancellationToken);

        // Save template content to MinIO
        var minioObjectPath = $"{_templatesFolderPath}{templateName}.json";
        await SaveTemplateContentToMinIOAsync(templateContent, minioObjectPath, cancellationToken);

        // Create template entity
        var template = new Template
        {
            Name = templateName,
            Description = description,
            SourceDomainId = sourceDomainId,
            SourceDatabaseName = sourceDatabaseName,
            Collections = collections,
            MinIOObjectPath = minioObjectPath,
            TotalDocumentCount = templateContent.Collections.Sum(c => c.Documents.Count),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        // Update document counts in collections
        foreach (var collection in template.Collections)
        {
            var contentCollection = templateContent.Collections
                .FirstOrDefault(c => c.CollectionName == collection.CollectionName);
            if (contentCollection != null)
            {
                collection.DocumentCount = contentCollection.Documents.Count;
            }
        }

        // Save to MongoDB
        await _templateRepository.AddAsync(template);

        _logger.LogInformation(
            "Template created successfully: {TemplateName}, {CollectionCount} collections, {DocumentCount} documents",
            templateName, collections.Count, template.TotalDocumentCount);

        return template;
    }

    public async Task<Template?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        return await _templateRepository.GetByNameAsync(templateName);
    }

    public async Task<IEnumerable<Template>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return await _templateRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Template>> GetTemplatesBySourceDomainAsync(string domainId, CancellationToken cancellationToken = default)
    {
        return await _templateRepository.GetBySourceDomainIdAsync(domainId);
    }

    public async Task<Template> UpdateTemplateAsync(
        string templateName,
        string? description,
        List<Domain.Entities.SelectedCollection> collections,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var existingTemplate = await _templateRepository.GetByNameAsync(templateName);
        if (existingTemplate == null)
        {
            throw new InvalidOperationException($"Template '{templateName}' not found");
        }

        // Recreate template content from source database
        var templateContent = await CreateTemplateContentAsync(
            templateName,
            existingTemplate.SourceDatabaseName,
            collections,
            cancellationToken);

        // Update template content in MinIO
        await SaveTemplateContentToMinIOAsync(templateContent, existingTemplate.MinIOObjectPath, cancellationToken);

        // Update template entity
        if (!string.IsNullOrEmpty(description))
        {
            existingTemplate.Description = description;
        }
        existingTemplate.Collections = collections;
        existingTemplate.TotalDocumentCount = templateContent.Collections.Sum(c => c.Documents.Count);
        existingTemplate.UpdatedAt = DateTime.UtcNow;
        existingTemplate.UpdatedBy = updatedBy;

        // Update document counts in collections
        foreach (var collection in existingTemplate.Collections)
        {
            var contentCollection = templateContent.Collections
                .FirstOrDefault(c => c.CollectionName == collection.CollectionName);
            if (contentCollection != null)
            {
                collection.DocumentCount = contentCollection.Documents.Count;
            }
        }

        // Update in MongoDB
        await _templateRepository.UpdateAsync(existingTemplate);

        _logger.LogInformation(
            "Template updated successfully: {TemplateName}, {CollectionCount} collections, {DocumentCount} documents",
            templateName, collections.Count, existingTemplate.TotalDocumentCount);

        return existingTemplate;
    }

    public async Task<bool> DeleteTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByNameAsync(templateName);
        if (template == null)
        {
            return false;
        }

        // Delete from MinIO
        try
        {
            // MinIO service doesn't have DeleteObjectAsync, but we can try to delete the bucket object
            // For now, we'll just delete from MongoDB
            // TODO: Add DeleteObjectAsync to IMinioService if needed
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete template content from MinIO: {TemplateName}", templateName);
        }

        // Delete from MongoDB
        await _templateRepository.DeleteAsync(template.Id);

        _logger.LogInformation("Template deleted successfully: {TemplateName}", templateName);
        return true;
    }

    public async Task<TemplateContent?> GetTemplateContentAsync(string templateName, CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByNameAsync(templateName);
        if (template == null)
        {
            return null;
        }

        // Read from MinIO
        var stream = await _minioService.GetObjectAsync(SystemBucketName, template.MinIOObjectPath, cancellationToken);
        if (stream == null)
        {
            _logger.LogWarning("Template content not found in MinIO: {TemplateName}", templateName);
            return null;
        }

        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            var content = JsonSerializer.Deserialize<TemplateContent>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize template content: {TemplateName}", templateName);
            return null;
        }
    }

    /// <summary>
    /// Create template content by reading collections from source database
    /// </summary>
    private async Task<TemplateContent> CreateTemplateContentAsync(
        string templateName,
        string sourceDatabaseName,
        List<Domain.Entities.SelectedCollection> collections,
        CancellationToken cancellationToken)
    {
        var templateContent = new TemplateContent
        {
            TemplateName = templateName,
            CreatedAt = DateTime.UtcNow,
            Collections = new List<CollectionData>()
        };

        var sourceDatabase = _mongoClient.GetDatabase(sourceDatabaseName);

        foreach (var selectedCollection in collections)
        {
            var collectionData = new CollectionData
            {
                CollectionName = selectedCollection.CollectionName,
                Documents = new List<Dictionary<string, object>>(),
                Indexes = new List<IndexDefinition>()
            };

            // Read all documents from collection
            var collection = sourceDatabase.GetCollection<BsonDocument>(selectedCollection.CollectionName);
            var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty)
                .ToListAsync(cancellationToken);

            // Convert BsonDocuments to Dictionaries
            collectionData.Documents = documents.ToDictionaryList();

            // Read indexes if requested
            if (selectedCollection.IncludeIndexes)
            {
                var indexes = await collection.Indexes.ListAsync(cancellationToken);
                var indexList = await indexes.ToListAsync(cancellationToken);

                foreach (var index in indexList)
                {
                    // Skip _id index
                    if (index["name"].AsString == "_id_")
                        continue;

                    var indexDef = new IndexDefinition
                    {
                        Keys = index["key"].AsBsonDocument.ToDictionary(),
                        Unique = index.Contains("unique") && index["unique"].AsBoolean,
                        Sparse = index.Contains("sparse") && index["sparse"].AsBoolean,
                        Background = index.Contains("background") && index["background"].AsBoolean,
                        Name = index["name"].AsString
                    };

                    collectionData.Indexes.Add(indexDef);
                }
            }

            templateContent.Collections.Add(collectionData);
        }

        return templateContent;
    }

    /// <summary>
    /// Save template content to MinIO as JSON
    /// </summary>
    private async Task SaveTemplateContentToMinIOAsync(
        TemplateContent templateContent,
        string objectPath,
        CancellationToken cancellationToken)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = JsonSerializer.Serialize(templateContent, jsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(jsonBytes);

        var success = await _minioService.PutObjectAsync(
            SystemBucketName,
            objectPath,
            stream,
            "application/json",
            cancellationToken);

        if (!success)
        {
            throw new InvalidOperationException($"Failed to save template content to MinIO: {objectPath}");
        }

        _logger.LogDebug("Template content saved to MinIO: {ObjectPath}", objectPath);
    }

    /// <summary>
    /// Ensure system bucket exists
    /// </summary>
    private async Task EnsureSystemBucketExistsAsync(CancellationToken cancellationToken)
    {
        var bucketExists = await _minioService.BucketExistsAsync(SystemBucketName);
        if (!bucketExists)
        {
            _logger.LogInformation("System bucket does not exist, creating: {BucketName}", SystemBucketName);
            await _minioService.CreateBucketAsync(SystemBucketName, cancellationToken);
            
            // Create folder structure
            // Use the same folder path as templates (without trailing slash and filename)
            var systemFolder = _settings.MinIO.SystemFolderPath ?? "System";
            var templatesFolder = $"{systemFolder}/templates";
            var folders = new[] { templatesFolder };
            await _minioService.CreateFolderStructureAsync(SystemBucketName, folders, cancellationToken);
            
            _logger.LogInformation("System bucket created successfully: {BucketName}", SystemBucketName);
        }
    }
}
