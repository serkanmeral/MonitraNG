using Microsoft.Extensions.DependencyInjection;
using MngAdmin.Application.Backup.Services;
using MngAdmin.Application.Services;
using MngAdmin.Persistence.Backup;
using MngAdmin.Persistence.Services;

namespace MngAdmin.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services)
    {
        // Health Check Service - Application health monitoring
        services.AddScoped<IHealthCheckService, HealthCheckService>();

        // Backup Service
        services.AddScoped<IBackupService, BackupService>();
    }
}
