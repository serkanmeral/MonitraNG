using Microsoft.Extensions.DependencyInjection;
using MngDataGateway.Application.Services;
using MngDataGateway.Persistence.Services;

namespace MngDataGateway.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services)
    {
        // MongoDB Context Service - JWT'den domain alıp doğru database'i seçer
        services.AddScoped<IMongoContextService, MongoContextService>();
        
        // User Info Service - JWT'den UserInfo nesnesi oluşturur
        services.AddScoped<IUserInfoService, UserInfoService>();
        
        // Dataset Category Service - @dataset_categories CRUD
        services.AddScoped<IDatasetCategoryService, DatasetCategoryService>();
        
        // Dataset Service - @datasets CRUD
        services.AddScoped<IDatasetService, DatasetService>();
        
        // Notification Service - RabbitMQ event publishing
        services.AddScoped<INotificationService, NotificationService>();
        
        // Validation Service - Data validation
        services.AddScoped<IValidationService, ValidationService>();
        
        // Incremental Field Service - Auto-increment field generation
        services.AddScoped<IIncrementalFieldService, IncrementalFieldService>();
        
        // Data Process Service - Defaults, metadata, collection/index management
        services.AddScoped<IDataProcessService, DataProcessService>();
        
        // Data Repository - MongoDB CRUD operations
        services.AddScoped<IDataRepository, DataRepository>();
        
        // Data Service - Main orchestrator for data operations
        services.AddScoped<IDataService, DataService>();
        
        // Health Check Service - Application health monitoring
        services.AddScoped<IHealthCheckService, HealthCheckService>();

        // Domain Lookup Service - DomainId'den domain name ve database name lookup
        // (Başka amaçlar için kullanılabilir, şimdilik tutuyoruz)
        services.AddSingleton<IDomainLookupService, DomainLookupService>();

        // Filter Parser - Parse RESTful filter query parameter
        services.AddScoped<FilterParser>();

        // Sort Parser - Parse MongoDB-style sort query parameter
        services.AddScoped<SortParser>();
    }
}

