using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;

namespace MngScheduler.Persistence.Repositories;

/// <summary>
/// Repository implementation for System Job operations
/// System jobs are stored in mng_keeper database → @scheduled_jobs collection
/// </summary>
public class SystemJobRepository : ISystemJobRepository
{
    private readonly IMongoClient _mongoClient;
    private readonly ILogger<SystemJobRepository> _logger;
    private readonly MngSchedulerSettings _settings;
    private readonly IMongoCollection<ScheduledJob> _collection;
    private readonly string _databaseName;

    public SystemJobRepository(
        IMongoClient mongoClient,
        ILogger<SystemJobRepository> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        _databaseName = _settings.MongoDB.KeeperDatabaseName ?? "mngkeeper";
        var database = _mongoClient.GetDatabase(_databaseName);
        _collection = database.GetCollection<ScheduledJob>("@scheduled_jobs");

        // Create indexes
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            // Unique index on jobId
            var jobIdIndex = new CreateIndexModel<ScheduledJob>(
                Builders<ScheduledJob>.IndexKeys.Ascending(x => x.JobId),
                new CreateIndexOptions { Unique = true, Name = "idx_jobId_unique" });

            // Index on isActive
            var isActiveIndex = new CreateIndexModel<ScheduledJob>(
                Builders<ScheduledJob>.IndexKeys.Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "idx_isActive" });

            // Index on jobType
            var jobTypeIndex = new CreateIndexModel<ScheduledJob>(
                Builders<ScheduledJob>.IndexKeys.Ascending(x => x.JobType),
                new CreateIndexOptions { Name = "idx_jobType" });

            // Index on createdAt (descending)
            var createdAtIndex = new CreateIndexModel<ScheduledJob>(
                Builders<ScheduledJob>.IndexKeys.Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "idx_createdAt_desc" });

            _collection.Indexes.CreateMany(new[] { jobIdIndex, isActiveIndex, jobTypeIndex, createdAtIndex });
            _logger.LogInformation("Indexes created for @scheduled_jobs collection");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating indexes for @scheduled_jobs collection (may already exist)");
        }
    }

    public async Task<IEnumerable<ScheduledJob>> GetActiveJobsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;

            // Tanılama: Hangi DB/collection ve toplam kaç döküman var (filtresiz)
            var totalInCollection = await _collection.CountDocumentsAsync(FilterDefinition<ScheduledJob>.Empty);
            _logger.LogDebug("System jobs DB: {DatabaseName}, collection @scheduled_jobs, total documents: {Total}",
                _databaseName, totalInCollection);

            // Filter: System jobs that are active
            // Accept both enum/int (0) and string "System" for jobType; both bool true and int 1 for isActive
            // in case documents were written by another service or with different serialization.
            // Note: StartDate, ExpireDate, MaxExecutionCount checks are done in ShouldExecute() method
            var jobTypeFilter = Builders<ScheduledJob>.Filter.Or(
                Builders<ScheduledJob>.Filter.Eq(x => x.JobType, JobType.System),
                Builders<ScheduledJob>.Filter.Eq("jobType", "System"));
            var isActiveFilter = Builders<ScheduledJob>.Filter.Or(
                Builders<ScheduledJob>.Filter.Eq(x => x.IsActive, true),
                Builders<ScheduledJob>.Filter.Eq("isActive", 1));
            var filter = Builders<ScheduledJob>.Filter.And(jobTypeFilter, isActiveFilter);

            var jobs = await _collection.Find(filter).ToListAsync();

            var allInCollection = await _collection.Find(FilterDefinition<ScheduledJob>.Empty).ToListAsync();
            if (allInCollection.Count > jobs.Count)
            {
                foreach (var doc in allInCollection)
                {
                    if (jobs.Any(j => j.JobId == doc.JobId))
                        continue;
                    _logger.LogInformation(
                        "[Scheduler] System job NOT scheduled: jobId={JobId} isActive={IsActive} expireDate={ExpireDate} startDate={StartDate}",
                        doc.JobId, doc.IsActive, doc.ExpireDate, doc.StartDate);
                }
            }

            // Runtime checks: only return jobs that should execute (StartDate, ExpireDate, MaxExecutionCount).
            var validJobs = new List<ScheduledJob>();
            foreach (var job in jobs)
            {
                if (job.ShouldExecute(now))
                {
                    validJobs.Add(job);
                    _logger.LogInformation(
                        "[Scheduler] System job scheduled in Quartz: jobId={JobId} cron={Cron}",
                        job.JobId, job.CronExpression);
                }
                else
                {
                    _logger.LogInformation(
                        "[Scheduler] System job skipped (isActive true but ShouldExecute=false): jobId={JobId} expireDate={ExpireDate} startDate={StartDate} maxExec={MaxExec} totalExec={TotalExec}",
                        job.JobId, job.ExpireDate, job.StartDate, job.MaxExecutionCount, job.TotalExecutionCount);
                }
            }

            _logger.LogInformation(
                "[Scheduler] System jobs: collection={TotalInCollection} isActiveFilter={IsActiveCount} quartzReady={QuartzCount}",
                allInCollection.Count, jobs.Count, validJobs.Count);
            return validJobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active system jobs");
            throw;
        }
    }

    public async Task<ScheduledJob?> GetJobByIdAsync(string jobId)
    {
        try
        {
            var filter = Builders<ScheduledJob>.Filter.And(
                Builders<ScheduledJob>.Filter.Eq(x => x.JobId, jobId),
                Builders<ScheduledJob>.Filter.Eq(x => x.JobType, JobType.System)
            );

            var job = await _collection.Find(filter).FirstOrDefaultAsync();
            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system job by ID: {JobId}", jobId);
            throw;
        }
    }

    public async Task<IEnumerable<ScheduledJob>> GetAllJobsAsync()
    {
        try
        {
            var filter = Builders<ScheduledJob>.Filter.Eq(x => x.JobType, JobType.System);
            var jobs = await _collection.Find(filter).ToListAsync();
            _logger.LogDebug("Retrieved {Count} system jobs", jobs.Count);
            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all system jobs");
            throw;
        }
    }

    public async Task<ScheduledJob> CreateJobAsync(ScheduledJob job)
    {
        try
        {
            // Ensure job type is System
            job.JobType = JobType.System;
            job.CreatedAt = DateTime.UtcNow;
            job.UpdatedAt = null;

            // Ensure POST requests have default payload if not provided
            job.EnsurePostPayload();

            await _collection.InsertOneAsync(job);
            _logger.LogInformation("Created system job: {JobId}", job.JobId);
            return job;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            _logger.LogError(ex, "Duplicate jobId: {JobId}", job.JobId);
            throw new Domain.Exceptions.MngSchedulerException($"Job with ID '{job.JobId}' already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating system job: {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<ScheduledJob> UpdateJobAsync(ScheduledJob job)
    {
        try
        {
            // First, get the existing job to preserve execution counts and other read-only fields
            var existingJob = await GetJobByIdAsync(job.JobId);
            if (existingJob == null)
            {
                throw new Domain.Exceptions.JobNotFoundException(job.JobId);
            }

            // Preserve execution counts — execution handlers increment before update; keep the higher values.
            job.TotalExecutionCount = Math.Max(job.TotalExecutionCount, existingJob.TotalExecutionCount);
            job.SuccessfulExecutionCount = Math.Max(job.SuccessfulExecutionCount, existingJob.SuccessfulExecutionCount);
            job.FailedExecutionCount = Math.Max(job.FailedExecutionCount, existingJob.FailedExecutionCount);
            job.CreatedAt = existingJob.CreatedAt;
            job.CreatedBy = existingJob.CreatedBy;
            if (job.LastExecution == null)
            {
                job.LastExecution = existingJob.LastExecution;
            }
            else if (existingJob.LastExecution != null &&
                     existingJob.LastExecution.ExecutedAt > job.LastExecution.ExecutedAt)
            {
                job.LastExecution = existingJob.LastExecution;
            }
            job.Id = existingJob.Id; // Preserve MongoDB ObjectId

            job.UpdatedAt = DateTime.UtcNow;

            // Ensure POST requests have default payload if not provided
            job.EnsurePostPayload();

            var filter = Builders<ScheduledJob>.Filter.And(
                Builders<ScheduledJob>.Filter.Eq(x => x.JobId, job.JobId),
                Builders<ScheduledJob>.Filter.Eq(x => x.JobType, JobType.System)
            );

            var options = new ReplaceOptions { IsUpsert = false };
            var result = await _collection.ReplaceOneAsync(filter, job, options);

            if (result.MatchedCount == 0)
            {
                throw new Domain.Exceptions.JobNotFoundException(job.JobId);
            }

            _logger.LogInformation("Updated system job: {JobId}", job.JobId);
            return job;
        }
        catch (Domain.Exceptions.JobNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system job: {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<bool> DeleteJobAsync(string jobId)
    {
        try
        {
            var filter = Builders<ScheduledJob>.Filter.And(
                Builders<ScheduledJob>.Filter.Eq(x => x.JobId, jobId),
                Builders<ScheduledJob>.Filter.Eq(x => x.JobType, JobType.System)
            );

            var result = await _collection.DeleteOneAsync(filter);

            if (result.DeletedCount > 0)
            {
                _logger.LogInformation("Deleted system job: {JobId}", jobId);
                return true;
            }

            _logger.LogWarning("System job not found for deletion: {JobId}", jobId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting system job: {JobId}", jobId);
            throw;
        }
    }

    public async Task<bool> JobExistsAsync(string jobId)
    {
        try
        {
            var filter = Builders<ScheduledJob>.Filter.And(
                Builders<ScheduledJob>.Filter.Eq(x => x.JobId, jobId),
                Builders<ScheduledJob>.Filter.Eq(x => x.JobType, JobType.System)
            );

            var count = await _collection.CountDocumentsAsync(filter);
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if system job exists: {JobId}", jobId);
            throw;
        }
    }
}
