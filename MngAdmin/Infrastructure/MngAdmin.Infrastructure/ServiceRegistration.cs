using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MngAdmin.Application.Backup.Services;
using MngAdmin.Application.Configuration;
using MngAdmin.Infrastructure.Backup.Services;

namespace MngAdmin.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // MinIO Backup Service
        services.AddScoped<IMinioBackupService, MinioBackupService>();

        // Database Backup Services
        // MongoDB Backup Service
        services.AddScoped<MongoBackupService>();

        // PostgreSQL Backup Service
        services.AddScoped<PostgresBackupService>();
        
        return services;
    }
}
