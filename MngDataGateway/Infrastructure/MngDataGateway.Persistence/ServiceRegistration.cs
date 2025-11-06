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
    }
}

