using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngDataGateway.Application.Configuration
{

    public class MngDataGatewaySettings
    {
        public ServerSettings Server { get; set; }
        public Mongodb MongoDB { get; set; }
        public Rabbitmq RabbitMQ { get; set; }
        public CertificateSettings CertificateSettings { get; set; }
        public string OpenApiServerPath { get; set; }
        public Actors Actors { get; set; }
        public HistorySettings History { get; set; } = new();
        public DeletedDataSettings DeletedData { get; set; } = new();
        public QuerySettings Query { get; set; } = new();
        public FileStorageSettings FileStorage { get; set; } = new();
    }

    public class ServerSettings
    {
        public string Host { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 5010;
        public string Scheme { get; set; } = "https";
    }
    public class CertificateSettings
    {
        public string DNS { get; set; }
        public string MNG_CERT_FILE { get; set; }
        public string MNG_KEY_FILE { get; set; }
        public string MNG_CERT_FILE_CONTENT { get; set; }
        public string MNG_KEY_FILE_CONTENT { get; set; }

    }

    public class Actors
    {
        public string MngKeeper { get; set; }
    }
    public class Mongodb
    {
        public string ConnectionString { get; set; }
        
        /// <summary>
        /// MngKeeper database name (default: "mngkeeper")
        /// Used for domain lookup service
        /// </summary>
        public string MngKeeperDatabaseName { get; set; } = "mngkeeper";
    }

    public class Rabbitmq
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }
    }

    public class HistorySettings
    {
        /// <summary>
        /// Maximum number of history entries per entity (default: 50)
        /// </summary>
        public int MaxHistoryEntries { get; set; } = 50;
    }

    public class DeletedDataSettings
    {
        /// <summary>
        /// Retention period for deleted data in days (default: 7)
        /// </summary>
        public int RetentionDays { get; set; } = 7;
    }

    public class QuerySettings
    {
        /// <summary>
        /// Maximum depth for relation expansion (default: 2)
        /// Can be overridden by ?deep query parameter
        /// </summary>
        public int RelationExpansionMaxDepth { get; set; } = 2;

        /// <summary>
        /// Default page size for list queries (default: 50)
        /// </summary>
        public int DefaultPageSize { get; set; } = 50;

        /// <summary>
        /// Maximum page size for list queries (default: 1000)
        /// </summary>
        public int MaxPageSize { get; set; } = 1000;
    }

    /// <summary>
    /// File storage configuration for MinIO and encryption
    /// </summary>
    public class FileStorageSettings
    {
        public MinIOSettings Minio { get; set; } = new();
        public EncryptionSettings Encryption { get; set; } = new();
        public CompressionSettings Compression { get; set; } = new();
        public ValidationSettings Validation { get; set; } = new();
        public RetrySettings Retry { get; set; } = new();
    }

    /// <summary>
    /// MinIO configuration
    /// </summary>
    public class MinIOSettings
    {
        public string Endpoint { get; set; } = "minio:9000";
        public string AccessKey { get; set; } = "minioadmin";
        public string SecretKey { get; set; } = "minioadmin";
        public string BucketName { get; set; } = "datasets";
        public bool UseSSL { get; set; } = false;
    }

    /// <summary>
    /// Encryption settings (AES-256-GCM)
    /// </summary>
    public class EncryptionSettings
    {
        public bool Enabled { get; set; } = true;
        public string Algorithm { get; set; } = "AES-256-GCM";
        
        /// <summary>
        /// Base64-encoded 256-bit encryption key
        /// Generate with: Convert.ToBase64String(new byte[32]) then randomize
        /// </summary>
        public string Key { get; set; } = string.Empty;
        
        public string KeyDerivation { get; set; } = "PBKDF2";
        public int Iterations { get; set; } = 10000;
        public int SaltLength { get; set; } = 16;
    }

    /// <summary>
    /// Compression settings (gzip)
    /// </summary>
    public class CompressionSettings
    {
        public string Algorithm { get; set; } = "gzip";
        
        /// <summary>
        /// Compression level: 1-9 (1=fast, 9=best)
        /// </summary>
        public int Level { get; set; } = 6;  // Default: balanced
        
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// File validation settings
    /// </summary>
    public class ValidationSettings
    {
        /// <summary>
        /// Maximum file size in bytes (default: 5MB)
        /// </summary>
        public long MaxFileSize { get; set; } = 5242880;

        /// <summary>
        /// Maximum folder depth in path (default: 10)
        /// </summary>
        public int MaxFolderDepth { get; set; } = 10;

        /// <summary>
        /// Maximum folder path length (default: 512)
        /// </summary>
        public int MaxPathLength { get; set; } = 512;

        /// <summary>
        /// Allowed file extensions (empty = all allowed)
        /// </summary>
        public List<string> AllowedExtensions { get; set; } = new()
        {
            ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".rtf",
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg",
            ".mp4", ".avi", ".mov", ".mkv",
            ".zip", ".rar", ".7z"
        };
    }

    /// <summary>
    /// Retry configuration for MinIO uploads
    /// </summary>
    public class RetrySettings
    {
        /// <summary>
        /// Maximum retry attempts (default: 3)
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Backoff delays in milliseconds for each attempt
        /// Default: [0, 1000, 2000]
        /// </summary>
        public List<int> BackoffDelayMs { get; set; } = new() { 0, 1000, 2000 };
    }

}

