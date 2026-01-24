using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MngDataGateway.Application.Configuration;
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
                var options = provider.GetRequiredService<IOptions<MngDataGatewaySettings>>();
                var settings = options.Value.FileStorage.Minio;

                var clientBuilder = new MinioClient()
                    .WithEndpoint(settings.Endpoint)
                    .WithCredentials(settings.AccessKey, settings.SecretKey);

                if (settings.UseSSL)
                {
                    clientBuilder = clientBuilder.WithSSL();
                }
                else
                {
                    clientBuilder = clientBuilder.WithSSL(false);
                }

                return clientBuilder.Build();
            });

            // File Validation Service (Scoped)
            services.AddScoped<IFileFieldValidator, FileFieldValidator>();

            // Compression Service (Scoped)
            services.AddScoped<IFileCompressionService>(provider =>
            {
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileCompressionService>>();
                var options = provider.GetRequiredService<IOptions<MngDataGatewaySettings>>();
                var compressionLevel = options.Value.FileStorage.Compression.Level;
                return new FileCompressionService(logger, compressionLevel);
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
