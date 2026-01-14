using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;

namespace MngScheduler.Persistence.Repositories;

/// <summary>
/// Repository implementation for Job Execution history operations
/// System job executions: mng_keeper → @job_executions collection
/// User job executions: Domain database → @job_executions dataset (MngDataGateway)
/// </summary>
public class JobExecutionRepository : IJobExecutionRepository
{
    private readonly IMongoClient _mongoClient;
    private readonly IMngDataGatewayClient _dataGatewayClient;
    private readonly IDomainLookupService _domainLookupService;
    private readonly ILogger<JobExecutionRepository> _logger;
    private readonly MngSchedulerSettings _settings;
    private readonly IMongoCollection<JobExecution> _systemExecutionsCollection;
    private const string UserExecutionsDataset = "@job_executions";

    public JobExecutionRepository(
        IMongoClient mongoClient,
        IMngDataGatewayClient dataGatewayClient,
        IDomainLookupService domainLookupService,
        ILogger<JobExecutionRepository> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        _dataGatewayClient = dataGatewayClient ?? throw new ArgumentNullException(nameof(dataGatewayClient));
        _domainLookupService = domainLookupService ?? throw new ArgumentNullException(nameof(domainLookupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        // System job executions collection
        var databaseName = _settings.MongoDB.KeeperDatabaseName ?? "mngkeeper";
        var database = _mongoClient.GetDatabase(databaseName);
        _systemExecutionsCollection = database.GetCollection<JobExecution>("@job_executions");

        // Create indexes
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            // Index on jobId
            var jobIdIndex = new CreateIndexModel<JobExecution>(
                Builders<JobExecution>.IndexKeys.Ascending(x => x.JobId),
                new CreateIndexOptions { Name = "idx_jobId" });

            // Index on executedAt (descending)
            var executedAtIndex = new CreateIndexModel<JobExecution>(
                Builders<JobExecution>.IndexKeys.Descending(x => x.ExecutedAt),
                new CreateIndexOptions { Name = "idx_executedAt_desc" });

            // TTL index (90 days)
            var ttlIndex = new CreateIndexModel<JobExecution>(
                Builders<JobExecution>.IndexKeys.Ascending(x => x.ExecutedAt),
                new CreateIndexOptions 
                { 
                    Name = "idx_executedAt_ttl",
                    ExpireAfter = TimeSpan.FromDays(90)
                });

            _systemExecutionsCollection.Indexes.CreateMany(new[] { jobIdIndex, executedAtIndex, ttlIndex });
            _logger.LogInformation("Indexes created for @job_executions collection");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating indexes for @job_executions collection (may already exist)");
        }
    }

    public async Task<JobExecution> SaveSystemJobExecutionAsync(JobExecution execution)
    {
        try
        {
            // Truncate response body if too large (max 10KB)
            if (!string.IsNullOrEmpty(execution.ResponseBody) && execution.ResponseBody.Length > 10240)
            {
                execution.ResponseBody = execution.ResponseBody.Substring(0, 10240) + "... [truncated]";
            }

            await _systemExecutionsCollection.InsertOneAsync(execution);
            _logger.LogDebug("Saved system job execution: {ExecutionId} for job {JobId}", 
                execution.ExecutionId, execution.JobId);
            return execution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving system job execution: {ExecutionId}", execution.ExecutionId);
            throw;
        }
    }

    public async Task<JobExecution> SaveUserJobExecutionAsync(string domainId, JobExecution execution, string? token = null)
    {
        try
        {
            // Set domainId
            execution.DomainId = domainId;

            // Truncate response body if too large (max 10KB)
            if (!string.IsNullOrEmpty(execution.ResponseBody) && execution.ResponseBody.Length > 10240)
            {
                execution.ResponseBody = execution.ResponseBody.Substring(0, 10240) + "... [truncated]";
            }

            var savedExecution = await _dataGatewayClient.CreateAsync<JobExecution>(
                UserExecutionsDataset, 
                execution, 
                token);

            _logger.LogDebug("Saved user job execution: {ExecutionId} for job {JobId} in domain {DomainId}", 
                execution.ExecutionId, execution.JobId, domainId);
            return savedExecution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user job execution: {ExecutionId} in domain {DomainId}", 
                execution.ExecutionId, domainId);
            throw;
        }
    }

    public async Task<IEnumerable<JobExecution>> GetSystemJobExecutionsAsync(string jobId, int limit = 100)
    {
        try
        {
            var filter = Builders<JobExecution>.Filter.Eq(x => x.JobId, jobId);
            var sort = Builders<JobExecution>.Sort.Descending(x => x.ExecutedAt);

            var executions = await _systemExecutionsCollection
                .Find(filter)
                .Sort(sort)
                .Limit(limit)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} executions for system job {JobId}", executions.Count, jobId);
            return executions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving executions for system job {JobId}", jobId);
            throw;
        }
    }

    public async Task<IEnumerable<JobExecution>> GetUserJobExecutionsAsync(
        string domainId, 
        string jobId, 
        int limit = 100, 
        string? token = null)
    {
        try
        {
            // Query: jobId = {jobId} AND domainId = {domainId}, sorted by executedAt descending
            var query = $"filter=jobId:{jobId},domainId:{domainId}&sort=executedAt:desc&limit={limit}";
            var executions = await _dataGatewayClient.GetAsync<JobExecution>(
                UserExecutionsDataset, 
                query, 
                token);

            _logger.LogDebug("Retrieved {Count} executions for user job {JobId} in domain {DomainId}", 
                executions.Count(), jobId, domainId);
            return executions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving executions for user job {JobId} in domain {DomainId}", 
                jobId, domainId);
            throw;
        }
    }

    public async Task<int> CleanupOldExecutionsAsync(TimeSpan retentionPeriod)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(retentionPeriod);
            var filter = Builders<JobExecution>.Filter.Lt(x => x.ExecutedAt, cutoffDate);

            var result = await _systemExecutionsCollection.DeleteManyAsync(filter);
            
            _logger.LogInformation("Cleaned up {Count} old system job executions (older than {RetentionPeriod} days)", 
                result.DeletedCount, retentionPeriod.TotalDays);

            // Note: User job executions cleanup would need to be done via MngDataGateway
            // or through a scheduled cleanup job

            return (int)result.DeletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old executions");
            throw;
        }
    }
}
