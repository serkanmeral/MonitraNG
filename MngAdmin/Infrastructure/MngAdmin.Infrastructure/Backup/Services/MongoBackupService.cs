using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAdmin.Application.Backup.Services;
using MngAdmin.Application.Configuration;

namespace MngAdmin.Infrastructure.Backup.Services;

/// <summary>
/// MongoDB backup service implementation
/// </summary>
public class MongoBackupService : IDatabaseBackupService
{
    private readonly ILogger<MongoBackupService> _logger;
    private readonly MongoBackupSettings _settings;

    public MongoBackupService(ILogger<MongoBackupService> logger, IOptions<MngAdminSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings.Value.Backup?.MongoDB ?? new MongoBackupSettings();
    }

    public async Task<Stream> CreateMongoBackupAsync(string databaseName, string connectionString, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating MongoDB backup for database: {DatabaseName}", databaseName);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Run mongodump
            // Parse connection string to extract components for mongodump
            // Format: mongodb://[username]:[password]@[host]:[port]
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo;
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 27017;
            var dumpPath = Path.Combine(tempDir, "dump");
            
            // Build mongodump arguments
            var arguments = $"--host {host} --port {port} --db {databaseName} --out \"{dumpPath}\"";
            
            // Add authentication if provided
            if (!string.IsNullOrEmpty(userInfo))
            {
                var parts = userInfo.Split(':');
                if (parts.Length == 2)
                {
                    var username = Uri.UnescapeDataString(parts[0]);
                    var password = Uri.UnescapeDataString(parts[1]);
                    arguments += $" --username \"{username}\" --password \"{password}\" --authenticationDatabase admin";
                }
            }
            
            // Log arguments without password for security
            var logArguments = arguments;
            if (!string.IsNullOrEmpty(userInfo))
            {
                var parts = userInfo.Split(':');
                if (parts.Length == 2)
                {
                    logArguments = logArguments.Replace($" --password \"{Uri.UnescapeDataString(parts[1])}\"", " --password ***");
                }
            }
            _logger.LogInformation("Running mongodump with arguments: {Arguments}", logArguments);
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "mongodump",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start mongodump process");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("mongodump failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                throw new InvalidOperationException($"mongodump failed: {error}");
            }

            _logger.LogInformation("mongodump completed successfully for database: {DatabaseName}", databaseName);

            // Create zip archive
            var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var databaseDumpPath = Path.Combine(dumpPath, databaseName);
                if (Directory.Exists(databaseDumpPath))
                {
                    await AddDirectoryToZipAsync(archive, databaseDumpPath, databaseName, cancellationToken);
                }
            }

            zipStream.Seek(0, SeekOrigin.Begin);
            _logger.LogInformation("MongoDB backup archive created: {Size} bytes", zipStream.Length);

            return zipStream;
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDir);
            }
        }
    }

    public Task<Stream> CreatePostgresBackupAsync(string databaseName, string connectionString, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("PostgreSQL backup is not supported by MongoBackupService");
    }

    private async Task AddDirectoryToZipAsync(ZipArchive archive, string directoryPath, string basePath, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.GetFiles(directoryPath))
        {
            var relativePath = Path.Combine(basePath, Path.GetFileName(file)).Replace('\\', '/');
            var entry = archive.CreateEntry(relativePath);
            
            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(file);
            await fileStream.CopyToAsync(entryStream, cancellationToken);
        }

        foreach (var subDir in Directory.GetDirectories(directoryPath))
        {
            var dirName = Path.GetFileName(subDir);
            var newBasePath = Path.Combine(basePath, dirName).Replace('\\', '/');
            await AddDirectoryToZipAsync(archive, subDir, newBasePath, cancellationToken);
        }
    }
}
