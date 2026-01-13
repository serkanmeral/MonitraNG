using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAdmin.Application.Backup.Services;
using MngAdmin.Application.Configuration;

namespace MngAdmin.Infrastructure.Backup.Services;

/// <summary>
/// PostgreSQL backup service implementation
/// </summary>
public class PostgresBackupService : IDatabaseBackupService
{
    private readonly ILogger<PostgresBackupService> _logger;
    private readonly PostgresBackupSettings _settings;

    public PostgresBackupService(ILogger<PostgresBackupService> logger, IOptions<MngAdminSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings.Value.Backup?.PostgreSQL ?? new PostgresBackupSettings();
    }

    public Task<Stream> CreateMongoBackupAsync(string databaseName, string connectionString, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("MongoDB backup is not supported by PostgresBackupService");
    }

    public async Task<Stream> CreatePostgresBackupAsync(string databaseName, string connectionString, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating PostgreSQL backup for database: {DatabaseName}", databaseName);

        // Find pg_dump executable
        // Priority: 1. Settings PgDumpPath, 2. PATH, 3. Common Windows paths, 4. Docker (PATH)
        var pgDumpPath = FindPgDumpPath();
        if (string.IsNullOrEmpty(pgDumpPath))
        {
            _logger.LogWarning("pg_dump command not found. In Docker, pg_dump is automatically available via Dockerfile. For local development, specify PgDumpPath in appsettings.json or ensure PostgreSQL is in PATH.");
            throw new InvalidOperationException("pg_dump command not found. PostgreSQL backups are only available in Docker environment where PostgreSQL client tools are automatically installed via Dockerfile. For local development, please specify PgDumpPath in appsettings.json or ensure PostgreSQL is in PATH.");
        }

        _logger.LogInformation("Using pg_dump: {PgDumpPath}", pgDumpPath);

        // Verify pg_dump is working
        try
        {
            var checkProcess = new ProcessStartInfo
            {
                FileName = pgDumpPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var checkProc = Process.Start(checkProcess);
            if (checkProc == null)
            {
                throw new InvalidOperationException("Failed to start pg_dump version check");
            }
            
            await checkProc.WaitForExitAsync(cancellationToken);
            if (checkProc.ExitCode != 0)
            {
                throw new InvalidOperationException("pg_dump version check failed");
            }
            
            var version = await checkProc.StandardOutput.ReadToEndAsync(cancellationToken);
            _logger.LogInformation("pg_dump version: {Version}", version.Trim());
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to execute pg_dump: {PgDumpPath}", pgDumpPath);
            throw new InvalidOperationException($"pg_dump not executable at {pgDumpPath}. Please verify the path is correct.");
        }

        // Parse connection string (format: Host=host;Port=port;Database=db;Username=user;Password=pass)
        var connectionParams = ParseConnectionString(connectionString);
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = pgDumpPath,
            Arguments = $"-h {connectionParams.Host} -p {connectionParams.Port} -U {connectionParams.Username} -d {databaseName} -F p",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = { ["PGPASSWORD"] = connectionParams.Password }
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start pg_dump process");
        }

        // Read output stream into memory stream
        var backupStream = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(backupStream, cancellationToken);
        
        // Read error output (non-blocking)
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        // Wait for process to complete
        await process.WaitForExitAsync(cancellationToken);
        
        // Wait for output stream to finish reading
        await outputTask;
        
        // Get error output
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("pg_dump failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
            backupStream.Dispose();
            throw new InvalidOperationException($"pg_dump failed: {error}");
        }

        // Verify backup stream has data
        if (backupStream.Length == 0)
        {
            _logger.LogWarning("pg_dump completed but produced empty output. Error output: {Error}", error);
            // Don't throw - empty database might produce empty backup
        }
        
        _logger.LogInformation("pg_dump completed successfully. Output size: {Size} bytes", backupStream.Length);

        // If compression is gzip, compress the stream
        if (_settings.Compression == "gzip")
        {
            var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress, leaveOpen: true))
            {
                backupStream.Seek(0, SeekOrigin.Begin);
                await backupStream.CopyToAsync(gzipStream, cancellationToken);
            }
            compressedStream.Seek(0, SeekOrigin.Begin);
            backupStream.Dispose();
            _logger.LogInformation("PostgreSQL backup compressed: {Size} bytes", compressedStream.Length);
            return compressedStream;
        }

        backupStream.Seek(0, SeekOrigin.Begin);
        _logger.LogInformation("PostgreSQL backup created: {Size} bytes", backupStream.Length);
        return backupStream;
    }

    private (string Host, string Port, string Username, string Password) ParseConnectionString(string connectionString)
    {
        var host = "localhost";
        var port = "5432";
        var username = "postgres";
        var password = "";

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length != 2) continue;

            var key = keyValue[0].Trim();
            var value = keyValue[1].Trim();

            switch (key.ToLowerInvariant())
            {
                case "host":
                case "server":
                    host = value;
                    break;
                case "port":
                    port = value;
                    break;
                case "username":
                case "user":
                case "uid":
                    username = value;
                    break;
                case "password":
                case "pwd":
                    password = value;
                    break;
            }
        }

        return (host, port, username, password);
    }

    /// <summary>
    /// Finds pg_dump executable path
    /// Priority: 1. Settings PgDumpPath, 2. PATH, 3. Common Windows paths
    /// In Docker, pg_dump will be in PATH via Dockerfile installation
    /// </summary>
    private string? FindPgDumpPath()
    {
        // 1. Check if PgDumpPath is specified in settings
        if (!string.IsNullOrEmpty(_settings.PgDumpPath))
        {
            if (File.Exists(_settings.PgDumpPath))
            {
                _logger.LogInformation("Using pg_dump from settings: {Path}", _settings.PgDumpPath);
                return _settings.PgDumpPath;
            }
            _logger.LogWarning("PgDumpPath specified in settings but file not found: {Path}", _settings.PgDumpPath);
        }

        // 2. Check PATH
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "pg_dump",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var proc = Process.Start(processStartInfo);
            if (proc != null)
            {
                proc.WaitForExit(1000); // Wait max 1 second
                if (proc.ExitCode == 0)
                {
                    _logger.LogInformation("Found pg_dump in PATH");
                    return "pg_dump"; // Return command name, will be resolved by Process.Start
                }
            }
        }
        catch
        {
            // pg_dump not in PATH, continue to next check
        }

        // 3. Check common Windows paths (for local development)
        if (OperatingSystem.IsWindows())
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\14\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\13\bin\pg_dump.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation("Found pg_dump at common Windows path: {Path}", path);
                    return path;
                }
            }
        }

        // 4. In Docker, pg_dump should be in PATH (installed via Dockerfile)
        // If we reach here and we're in Docker, something is wrong
        // But we'll let the error be thrown above

        return null;
    }
}
