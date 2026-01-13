namespace MngAdmin.Application.Configuration;

public class MngAdminSettings
{
    public ServerSettings Server { get; set; } = new();
    public Mongodb MongoDB { get; set; } = new();
    public Rabbitmq RabbitMQ { get; set; } = new();
    public CertificateSettings CertificateSettings { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    public Actors Actors { get; set; } = new();
    public BackupSettings Backup { get; set; } = new();
    public MinIOSettings MinIO { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5080;
    public string Scheme { get; set; } = "https";
}

public class CertificateSettings
{
    public string DNS { get; set; } = string.Empty;
    public string MNG_CERT_FILE { get; set; } = string.Empty;
    public string MNG_KEY_FILE { get; set; } = string.Empty;
    public string MNG_CERT_FILE_CONTENT { get; set; } = string.Empty;
    public string MNG_KEY_FILE_CONTENT { get; set; } = string.Empty;
}

public class Actors
{
    public string MngKeeper { get; set; } = string.Empty;
}

public class Mongodb
{
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// MngKeeper database name (default: "mngkeeper")
    /// Used for domain lookup service
    /// </summary>
    public string MngKeeperDatabaseName { get; set; } = "mngkeeper";
}

public class Rabbitmq
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
}

public class BackupSettings
{
    /// <summary>
    /// Maximum number of backups to keep per database (default: 10)
    /// </summary>
    public int MaxBackupCount { get; set; } = 10;
    
    public MinIOBackupSettings MinIO { get; set; } = new();
    
    public MongoBackupSettings MongoDB { get; set; } = new();
    
    public PostgresBackupSettings PostgreSQL { get; set; } = new();
}

public class MinIOBackupSettings
{
    /// <summary>
    /// System bucket name (default: "system")
    /// </summary>
    public string SystemBucket { get; set; } = "system";
    
    /// <summary>
    /// System backup path within system bucket (default: "backup")
    /// </summary>
    public string SystemBackupPath { get; set; } = "backup";
    
    /// <summary>
    /// Domain backup path within domain bucket (default: "backups")
    /// </summary>
    public string DomainBackupPath { get; set; } = "backups";
}

public class MongoBackupSettings
{
    /// <summary>
    /// Compression format for MongoDB backups (default: "zip")
    /// </summary>
    public string Compression { get; set; } = "zip";
    
    /// <summary>
    /// Include system collections in backup (default: false)
    /// </summary>
    public bool IncludeSystemCollections { get; set; } = false;
}

public class PostgresBackupSettings
{
    /// <summary>
    /// Compression format for PostgreSQL backups (default: "gzip")
    /// </summary>
    public string Compression { get; set; } = "gzip";
    
    /// <summary>
    /// List of PostgreSQL databases to backup (default: ["keycloak"])
    /// </summary>
    public List<string> Databases { get; set; } = new() { "keycloak" };
    
    /// <summary>
    /// PostgreSQL connection string template.
    /// Format: "Host={host};Port={port};Database={database};Username={username};Password={password}"
    /// In Docker: "Host=postgres;Port=5432;Database={database};Username=keycloak;Password=keycloak123"
    /// For local development: "Host=localhost;Port=5432;Database={database};Username=keycloak;Password=keycloak123"
    /// The {database} placeholder will be replaced with the actual database name during backup.
    /// </summary>
    public string ConnectionString { get; set; } = "Host=postgres;Port=5432;Database={database};Username=keycloak;Password=keycloak123";
    
    /// <summary>
    /// Optional path to pg_dump executable. If not specified, will search in PATH.
    /// Useful for local development when PostgreSQL is not in PATH.
    /// In Docker, pg_dump is automatically available in PATH via Dockerfile installation.
    /// Examples: "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe" (Windows) or "/usr/bin/pg_dump" (Linux)
    /// </summary>
    public string? PgDumpPath { get; set; }
}

public class MinIOSettings
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSSL { get; set; } = false;
    public string Region { get; set; } = "us-east-1";
}
