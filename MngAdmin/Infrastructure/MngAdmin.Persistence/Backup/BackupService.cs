using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MngAdmin.Application.Backup.DTOs;
using MngAdmin.Application.Backup.Services;
using MngAdmin.Application.Configuration;
using MngAdmin.Domain.Entities;
using MngAdmin.Infrastructure.Backup.Services;
using System.Diagnostics;
using MongoDB.Bson;

namespace MngAdmin.Persistence.Backup;

/// <summary>
/// Backup service implementation
/// </summary>
public class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly IMongoClient _mongoClient;
    private readonly MngAdminSettings _settings;
    private readonly IMinioBackupService _minioService;
    private readonly MongoBackupService _mongoBackupService;
    private readonly PostgresBackupService _postgresBackupService;
    private const string BackupStatusCollection = "backup_status";

    public BackupService(
        ILogger<BackupService> logger,
        IMongoClient mongoClient,
        IOptions<MngAdminSettings> settings,
        IMinioBackupService minioService,
        MongoBackupService mongoBackupService,
        PostgresBackupService postgresBackupService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _minioService = minioService ?? throw new ArgumentNullException(nameof(minioService));
        _mongoBackupService = mongoBackupService ?? throw new ArgumentNullException(nameof(mongoBackupService));
        _postgresBackupService = postgresBackupService ?? throw new ArgumentNullException(nameof(postgresBackupService));
    }

    private IMongoCollection<BackupStatus> GetBackupStatusCollection()
    {
        var db = _mongoClient.GetDatabase(_settings.MongoDB.MngKeeperDatabaseName);
        return db.GetCollection<BackupStatus>(BackupStatusCollection);
    }

    public async Task<BackupResponseDto> CreateSystemBackupAsync(BackupRequestDto request, string userId, CancellationToken cancellationToken = default)
    {
        var databaseType = request.DatabaseType?.ToLowerInvariant() ?? "mongodb";
        var databases = new List<string>();

        if (databaseType == "mongodb")
        {
            databases = new List<string> { "mngkeeper", "mngtemplates" };
            if (!string.IsNullOrEmpty(request.DatabaseName))
            {
                databases = databases.Where(d => d == request.DatabaseName).ToList();
            }
        }
        else if (databaseType == "postgresql")
        {
            databases = _settings.Backup?.PostgreSQL?.Databases ?? new List<string> { "keycloak" };
            if (!string.IsNullOrEmpty(request.DatabaseName))
            {
                databases = databases.Where(d => d == request.DatabaseName).ToList();
            }
        }

        if (databases.Count == 0)
        {
            throw new ArgumentException("No databases found to backup");
        }

        // For now, backup first database only (can be extended to backup all)
        var databaseName = databases.First();
        return await CreateBackupAsync("system", null, databaseName, databaseType, userId, cancellationToken);
    }

    public async Task<BackupResponseDto> CreateDomainBackupAsync(string domainName, BackupRequestDto request, string userId, CancellationToken cancellationToken = default)
    {
        // Validate domain exists by checking if database exists in MongoDB
        // Domain databases follow the pattern: mng_{domainName}
        var databaseName = $"mng_{domainName.ToLowerInvariant()}";
        
        // Get list of databases from MongoDB
        var databases = await _mongoClient.ListDatabaseNamesAsync(cancellationToken);
        var databaseList = await databases.ToListAsync(cancellationToken);
        
        // Check if domain database exists
        if (!databaseList.Contains(databaseName))
        {
            throw new ArgumentException($"Domain database not found: {databaseName}. Available domain databases: {string.Join(", ", databaseList.Where(d => d.StartsWith("mng_", StringComparison.OrdinalIgnoreCase)))}");
        }

        return await CreateBackupAsync("domain", domainName, databaseName, "mongodb", userId, cancellationToken);
    }

    private async Task<BackupResponseDto> CreateBackupAsync(
        string backupType,
        string? domainName,
        string databaseName,
        string databaseType,
        string userId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var backupStatus = new BackupStatus
        {
            Type = backupType,
            DatabaseName = databaseName,
            DomainName = domainName,
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        var collection = GetBackupStatusCollection();
        await collection.InsertOneAsync(backupStatus, cancellationToken: cancellationToken);

        try
        {
            _logger.LogInformation("Starting {Type} backup for database: {DatabaseName}", backupType, databaseName);

            // Create backup stream
            Stream backupStream;
            string fileExtension;
            string contentType;

            if (databaseType == "mongodb")
            {
                var connectionString = _settings.MongoDB.ConnectionString;
                backupStream = await _mongoBackupService.CreateMongoBackupAsync(databaseName, connectionString, cancellationToken);
                fileExtension = "zip";
                contentType = "application/zip";
            }
            else if (databaseType == "postgresql")
            {
                // PostgreSQL connection string from settings
                var connectionStringTemplate = _settings.Backup?.PostgreSQL?.ConnectionString ?? "Host=postgres;Port=5432;Database={database};Username=keycloak;Password=keycloak123";
                var connectionString = connectionStringTemplate.Replace("{database}", databaseName);
                backupStream = await _postgresBackupService.CreatePostgresBackupAsync(databaseName, connectionString, cancellationToken);
                fileExtension = "sql.gz";
                contentType = "application/gzip";
            }
            else
            {
                throw new NotSupportedException($"Database type not supported: {databaseType}");
            }

            // Generate backup file name
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{databaseName}_{timestamp}.{fileExtension}";

            // Determine backup path
            string bucketName;
            string backupPath;

            if (backupType == "system")
            {
                bucketName = _settings.Backup?.MinIO?.SystemBucket ?? "system";
                var basePath = _settings.Backup?.MinIO?.SystemBackupPath ?? "backup";
                var dbTypePath = databaseType == "mongodb" ? "mongodb" : "postgresql";
                backupPath = $"{basePath}/{dbTypePath}/{fileName}";
            }
            else
            {
                // Ensure domain name is lowercase for bucket name (MinIO bucket naming: mng-{domainName})
                bucketName = $"mng-{domainName?.ToLowerInvariant()}";
                // Domain backups go to root/backups folder (not system/backup)
                var basePath = _settings.Backup?.MinIO?.DomainBackupPath ?? "backups";
                backupPath = $"{basePath}/mongodb/{fileName}";
            }

            // Upload to MinIO
            var uploadSuccess = await _minioService.UploadBackupAsync(bucketName, backupPath, backupStream, contentType, cancellationToken);
            if (!uploadSuccess)
            {
                throw new InvalidOperationException($"Failed to upload backup to MinIO: {bucketName}/{backupPath}");
            }

            // Verify backup file exists in MinIO and get file size
            _logger.LogInformation("Verifying backup file in MinIO: {BucketName}/{BackupPath}", bucketName, backupPath);
            var backupInfo = await _minioService.GetBackupInfoAsync(bucketName, backupPath, cancellationToken);
            if (backupInfo == null)
            {
                _logger.LogWarning("Backup file not found in MinIO after upload: {BucketName}/{BackupPath}", bucketName, backupPath);
                throw new InvalidOperationException($"Backup file not found in MinIO after upload: {bucketName}/{backupPath}");
            }
            
            var sizeBytes = backupInfo.SizeBytes;
            _logger.LogInformation("Backup file verified in MinIO: {BucketName}/{BackupPath} ({Size} bytes)", bucketName, backupPath, sizeBytes);

            stopwatch.Stop();

            // Update backup status
            backupStatus.Status = "completed";
            backupStatus.CompletedAt = DateTime.UtcNow;
            backupStatus.DurationMs = stopwatch.ElapsedMilliseconds;
            backupStatus.SizeBytes = sizeBytes;
            backupStatus.BackupPath = $"{bucketName}/{backupPath}";

            var update = Builders<BackupStatus>.Update
                .Set(b => b.Status, backupStatus.Status)
                .Set(b => b.CompletedAt, backupStatus.CompletedAt)
                .Set(b => b.DurationMs, backupStatus.DurationMs)
                .Set(b => b.SizeBytes, backupStatus.SizeBytes)
                .Set(b => b.BackupPath, backupStatus.BackupPath);

            await collection.UpdateOneAsync(
                Builders<BackupStatus>.Filter.Eq(b => b.Id, backupStatus.Id),
                update,
                cancellationToken: cancellationToken);

            // Apply retention policy
            await ApplyRetentionPolicyAsync(backupType, domainName, databaseName, bucketName, backupPath, cancellationToken);

            _logger.LogInformation("Backup completed successfully: {DatabaseName} ({Size} bytes, {Duration}ms)",
                databaseName, sizeBytes, stopwatch.ElapsedMilliseconds);

            return MapToDto(backupStatus);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Backup failed for database: {DatabaseName}", databaseName);

            // Update backup status
            backupStatus.Status = "failed";
            backupStatus.CompletedAt = DateTime.UtcNow;
            backupStatus.DurationMs = stopwatch.ElapsedMilliseconds;
            backupStatus.ErrorMessage = ex.Message;

            var update = Builders<BackupStatus>.Update
                .Set(b => b.Status, backupStatus.Status)
                .Set(b => b.CompletedAt, backupStatus.CompletedAt)
                .Set(b => b.DurationMs, backupStatus.DurationMs)
                .Set(b => b.ErrorMessage, backupStatus.ErrorMessage);

            await collection.UpdateOneAsync(
                Builders<BackupStatus>.Filter.Eq(b => b.Id, backupStatus.Id),
                update,
                cancellationToken: cancellationToken);

            // Delete failed backup file if exists
            if (!string.IsNullOrEmpty(backupStatus.BackupPath))
            {
                var pathParts = backupStatus.BackupPath.Split('/', 2);
                if (pathParts.Length == 2)
                {
                    await _minioService.DeleteBackupAsync(pathParts[0], pathParts[1], cancellationToken);
                }
            }

            throw;
        }
    }

    private async Task ApplyRetentionPolicyAsync(
        string backupType,
        string? domainName,
        string databaseName,
        string bucketName,
        string currentBackupPath,
        CancellationToken cancellationToken)
    {
        var maxCount = _settings.Backup?.MaxBackupCount ?? 10;
        
        // Get backups from BackupStatus collection instead of MinIO directly
        var collection = GetBackupStatusCollection();
        var filter = Builders<BackupStatus>.Filter.And(
            Builders<BackupStatus>.Filter.Eq(b => b.Type, backupType),
            Builders<BackupStatus>.Filter.Eq(b => b.DatabaseName, databaseName),
            Builders<BackupStatus>.Filter.Eq(b => b.Status, "completed"));
        
        if (backupType == "domain" && !string.IsNullOrEmpty(domainName))
        {
            filter = Builders<BackupStatus>.Filter.And(
                filter,
                Builders<BackupStatus>.Filter.Eq(b => b.DomainName, domainName));
        }

        var backups = await collection.Find(filter)
            .SortByDescending(b => b.StartedAt)
            .ToListAsync(cancellationToken);

        if (backups.Count > maxCount)
        {
            var toDelete = backups.Skip(maxCount);
            foreach (var backup in toDelete)
            {
                _logger.LogInformation("Deleting old backup: {BackupPath}", backup.BackupPath);
                
                // Delete from MinIO
                var pathParts = backup.BackupPath.Split('/', 2);
                if (pathParts.Length == 2)
                {
                    await _minioService.DeleteBackupAsync(pathParts[0], pathParts[1], cancellationToken);
                }
                
                // Delete from BackupStatus collection
                await collection.DeleteOneAsync(
                    Builders<BackupStatus>.Filter.Eq(b => b.Id, backup.Id),
                    cancellationToken);
            }
        }
    }

    public async Task<BackupResponseDto?> GetBackupStatusAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var collection = GetBackupStatusCollection();
        var backup = await collection.Find(b => b.Id == backupId).FirstOrDefaultAsync(cancellationToken);
        return backup != null ? MapToDto(backup) : null;
    }

    public async Task<BackupListResponseDto> GetSystemBackupsAsync(string? databaseName = null, CancellationToken cancellationToken = default)
    {
        var collection = GetBackupStatusCollection();
        var filter = Builders<BackupStatus>.Filter.Eq(b => b.Type, "system");
        
        if (!string.IsNullOrEmpty(databaseName))
        {
            filter = Builders<BackupStatus>.Filter.And(
                filter,
                Builders<BackupStatus>.Filter.Eq(b => b.DatabaseName, databaseName));
        }

        var backups = await collection.Find(filter)
            .SortByDescending(b => b.StartedAt)
            .ToListAsync(cancellationToken);

        return new BackupListResponseDto
        {
            Backups = backups.Select(MapToDto).ToList(),
            TotalCount = backups.Count
        };
    }

    public async Task<BackupListResponseDto> GetDomainBackupsAsync(string domainName, string? databaseName = null, CancellationToken cancellationToken = default)
    {
        var collection = GetBackupStatusCollection();
        var filter = Builders<BackupStatus>.Filter.And(
            Builders<BackupStatus>.Filter.Eq(b => b.Type, "domain"),
            Builders<BackupStatus>.Filter.Eq(b => b.DomainName, domainName));

        if (!string.IsNullOrEmpty(databaseName))
        {
            filter = Builders<BackupStatus>.Filter.And(
                filter,
                Builders<BackupStatus>.Filter.Eq(b => b.DatabaseName, databaseName));
        }

        var backups = await collection.Find(filter)
            .SortByDescending(b => b.StartedAt)
            .ToListAsync(cancellationToken);

        return new BackupListResponseDto
        {
            Backups = backups.Select(MapToDto).ToList(),
            TotalCount = backups.Count
        };
    }

    public async Task<FullBackupResponseDto> CreateFullBackupAsync(string userId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var fullBackupId = ObjectId.GenerateNewId().ToString();
        var response = new FullBackupResponseDto
        {
            Id = fullBackupId,
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            SystemBackups = new List<BackupResponseDto>(),
            DomainBackups = new List<BackupResponseDto>(),
            DomainsBackedUp = new List<string>()
        };

        _logger.LogInformation("Starting full backup operation: {FullBackupId}", fullBackupId);

        try
        {
            // Step 1: System MongoDB backups
            _logger.LogInformation("Step 1: Creating system MongoDB backups");
            var systemMongoDatabases = new List<string> { "mngkeeper", "mngtemplates" };
            foreach (var dbName in systemMongoDatabases)
            {
                try
                {
                    var backupRequest = new BackupRequestDto { DatabaseType = "mongodb", DatabaseName = dbName };
                    var result = await CreateSystemBackupAsync(backupRequest, userId, cancellationToken);
                    response.SystemBackups.Add(result);
                    _logger.LogInformation("System MongoDB backup completed: {DatabaseName} - {Status}", dbName, result.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup system MongoDB database: {DatabaseName}", dbName);
                    response.SystemBackups.Add(new BackupResponseDto
                    {
                        Type = "system",
                        DatabaseName = dbName,
                        Status = "failed",
                        ErrorMessage = ex.Message,
                        StartedAt = DateTime.UtcNow
                    });
                }
            }

            // Step 2: System PostgreSQL backups
            _logger.LogInformation("Step 2: Creating system PostgreSQL backups");
            var postgresDatabases = _settings.Backup?.PostgreSQL?.Databases ?? new List<string> { "keycloak" };
            foreach (var dbName in postgresDatabases)
            {
                try
                {
                    var backupRequest = new BackupRequestDto { DatabaseType = "postgresql", DatabaseName = dbName };
                    var result = await CreateSystemBackupAsync(backupRequest, userId, cancellationToken);
                    response.SystemBackups.Add(result);
                    _logger.LogInformation("System PostgreSQL backup completed: {DatabaseName} - {Status}", dbName, result.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup system PostgreSQL database: {DatabaseName}", dbName);
                    response.SystemBackups.Add(new BackupResponseDto
                    {
                        Type = "system",
                        DatabaseName = dbName,
                        Status = "failed",
                        ErrorMessage = ex.Message,
                        StartedAt = DateTime.UtcNow
                    });
                }
            }

            // Step 3: Get all MongoDB databases and find domain databases (mng_*)
            _logger.LogInformation("Step 3: Discovering domain databases");
            var databases = await _mongoClient.ListDatabaseNamesAsync(cancellationToken);
            var databaseList = await databases.ToListAsync(cancellationToken);
            
            // Filter domain databases (mng_* pattern, excluding system databases)
            var systemDbNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mngkeeper", "mngtemplates", "admin", "config", "local" };
            var domainDatabases = databaseList
                .Where(db => db.StartsWith("mng_", StringComparison.OrdinalIgnoreCase) && !systemDbNames.Contains(db))
                .ToList();

            _logger.LogInformation("Found {Count} domain databases: {Databases}", domainDatabases.Count, string.Join(", ", domainDatabases));

            // Step 4: Create backups for each domain
            _logger.LogInformation("Step 4: Creating domain backups");
            foreach (var domainDbName in domainDatabases)
            {
                // Parse domain name from database name (mng_meral -> meral)
                var domainName = domainDbName.Substring(4); // Remove "mng_" prefix
                
                try
                {
                    var backupRequest = new BackupRequestDto { DatabaseType = "mongodb" };
                    var result = await CreateDomainBackupAsync(domainName, backupRequest, userId, cancellationToken);
                    response.DomainBackups.Add(result);
                    response.DomainsBackedUp.Add(domainName);
                    _logger.LogInformation("Domain backup completed: {DomainName} ({DatabaseName}) - {Status}", domainName, domainDbName, result.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup domain: {DomainName} ({DatabaseName})", domainName, domainDbName);
                    response.DomainBackups.Add(new BackupResponseDto
                    {
                        Type = "domain",
                        DomainName = domainName,
                        DatabaseName = domainDbName,
                        Status = "failed",
                        ErrorMessage = ex.Message,
                        StartedAt = DateTime.UtcNow
                    });
                }
            }

            stopwatch.Stop();
            response.CompletedAt = DateTime.UtcNow;
            response.DurationMs = stopwatch.ElapsedMilliseconds;
            response.TotalBackups = response.SystemBackups.Count + response.DomainBackups.Count;
            response.SuccessfulBackups = response.SystemBackups.Count(b => b.Status == "completed") + 
                                         response.DomainBackups.Count(b => b.Status == "completed");
            response.FailedBackups = response.SystemBackups.Count(b => b.Status == "failed") + 
                                     response.DomainBackups.Count(b => b.Status == "failed");
            response.Status = response.FailedBackups == 0 ? "completed" : "completed_with_errors";

            _logger.LogInformation("Full backup operation completed: {TotalBackups} backups, {Successful} successful, {Failed} failed, Duration: {Duration}ms",
                response.TotalBackups, response.SuccessfulBackups, response.FailedBackups, response.DurationMs);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Full backup operation failed");
            
            response.CompletedAt = DateTime.UtcNow;
            response.DurationMs = stopwatch.ElapsedMilliseconds;
            response.Status = "failed";
            response.ErrorMessage = ex.Message;
            
            return response;
        }
    }

    private static BackupResponseDto MapToDto(BackupStatus backup)
    {
        return new BackupResponseDto
        {
            Id = backup.Id,
            Type = backup.Type,
            DatabaseName = backup.DatabaseName,
            DomainName = backup.DomainName,
            Status = backup.Status,
            StartedAt = backup.StartedAt,
            CompletedAt = backup.CompletedAt,
            DurationMs = backup.DurationMs,
            SizeBytes = backup.SizeBytes,
            ErrorMessage = backup.ErrorMessage,
            BackupPath = backup.BackupPath
        };
    }
}
