using Microsoft.Extensions.DependencyInjection;
using MngDataGateway.Application.Interfaces;
using MngDataGateway.Application.Services;
using MngDataGateway.Application.Services.Files;
using MngDataGateway.Infrastructure.Services;
using MngDataGateway.Infrastructure.Services.Files;
using MngDataGateway.Infrastructure.Services.RabbitMq;
using Minio;

namespace MngDataGateway.Infrastructure
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // RabbitMQ Service (Singleton - one connection per app instance)
            services.AddSingleton<IRabbitMqService, RabbitMqService>();

            // Event Publisher (MngKeeper-style) - Scoped for per-request usage
            services.AddScoped<IEventPublisher, EventPublisher>();

            // File Storage Services
            AddFileStorageServices(services);

            return services;
        }

        /// <summary>
        /// Registers file storage services for file field type support
        /// </summary>
        private static void AddFileStorageServices(IServiceCollection services)
        {
            // MinIO Client (Singleton)
            services.AddSingleton<IMinioClient>(provider =>
            {
                // Note: Actual configuration will be injected from IOptions<MngDataGatewaySettings>
                // This is a placeholder; the real configuration happens in Program.cs
                return new MinioClient();
            });

            // File Validation Service (Scoped)
            services.AddScoped<IFileFieldValidator, FileFieldValidator>();

            // Compression Service (Scoped)
            services.AddScoped<IFileCompressionService>(provider =>
            {
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileCompressionService>>();
                return new FileCompressionService(logger, 6);  // Compression level 6 (default)
            });

            // Encryption Service (Scoped)
            services.AddScoped<IFileEncryptionService, FileEncryptionService>();

            // MinIO File Service (Scoped)
            services.AddScoped<IMinIOFileService, MinIOFileService>();

            // File Processing Pipeline (Scoped)
            services.AddScoped<IFileProcessingPipeline, FileProcessingPipeline>();
        }
    }
}
