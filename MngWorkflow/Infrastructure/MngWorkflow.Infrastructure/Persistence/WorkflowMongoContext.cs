using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MngWorkflow.Application.Configuration;

namespace MngWorkflow.Infrastructure.Persistence;

public interface IWorkflowMongoContext
{
    IMongoDatabase GetDatabase(string domainName);
}

public sealed class WorkflowMongoContext : IWorkflowMongoContext
{
    private readonly IMongoClient _client;
    private readonly string _prefix;
    private readonly ConcurrentDictionary<string, IMongoDatabase> _databases = new(StringComparer.OrdinalIgnoreCase);

    public WorkflowMongoContext(IMongoClient client, IOptions<MngWorkflowSettings> settings)
    {
        _client = client;
        _prefix = settings.Value.MongoDb.DatabasePrefix;
    }

    public IMongoDatabase GetDatabase(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
            throw new ArgumentException("Domain name is required.", nameof(domainName));

        return _databases.GetOrAdd(domainName, name => _client.GetDatabase($"{_prefix}{name}"));
    }
}

public static class WorkflowMongoServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowMongo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMongoClient>(_ =>
        {
            var connectionString = configuration
                .GetSection(MngWorkflowSettings.SectionName)
                .Get<MngWorkflowSettings>()?.MongoDb.ConnectionString ?? "mongodb://localhost:27017";
            return new MongoClient(connectionString);
        });

        services.AddSingleton<IWorkflowMongoContext, WorkflowMongoContext>();
        services.AddSingleton<WorkflowIndexInitializer>();
        return services;
    }
}
