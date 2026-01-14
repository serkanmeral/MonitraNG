using Microsoft.Extensions.DependencyInjection;
using MngScheduler.Application.Interfaces;
using MngScheduler.Persistence.Repositories;
using MngScheduler.Persistence.Services;

namespace MngScheduler.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services)
    {
        // System Job Repository
        services.AddScoped<ISystemJobRepository, SystemJobRepository>();

        // User Job Repository
        services.AddScoped<IUserJobRepository, Repositories.UserJobRepository>();

        // Job Execution Repository
        services.AddScoped<IJobExecutionRepository, Repositories.JobExecutionRepository>();

        // Domain Lookup Service
        services.AddScoped<IDomainLookupService, DomainLookupService>();
    }
}
