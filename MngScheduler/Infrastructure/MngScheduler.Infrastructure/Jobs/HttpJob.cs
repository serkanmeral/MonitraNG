using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;
using Quartz;

namespace MngScheduler.Infrastructure.Jobs;

/// <summary>
/// Generic HTTP Job implementation for Quartz.NET
/// Supports both GET and POST HTTP methods
/// </summary>
[DisallowConcurrentExecution] // Prevent multiple instances of the same job from running concurrently
public class HttpJob : IJob
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly ISystemJobRepository _systemJobRepository;
    private readonly IUserJobRepository _userJobRepository;
    private readonly IDomainLookupService _domainLookupService;
    private readonly IRabbitMqEventPublisher _eventPublisher;
    private readonly IMngKeeperAuthClient _keeperAuth;
    private readonly ILogger<HttpJob> _logger;
    private readonly MngSchedulerSettings _settings;

    public HttpJob(
        IHttpClientFactory httpClientFactory,
        IJobExecutionRepository executionRepository,
        ISystemJobRepository systemJobRepository,
        IUserJobRepository userJobRepository,
        IDomainLookupService domainLookupService,
        IRabbitMqEventPublisher eventPublisher,
        IMngKeeperAuthClient keeperAuth,
        ILogger<HttpJob> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _systemJobRepository = systemJobRepository ?? throw new ArgumentNullException(nameof(systemJobRepository));
        _userJobRepository = userJobRepository ?? throw new ArgumentNullException(nameof(userJobRepository));
        _domainLookupService = domainLookupService ?? throw new ArgumentNullException(nameof(domainLookupService));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _keeperAuth = keeperAuth ?? throw new ArgumentNullException(nameof(keeperAuth));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var executionId = Guid.NewGuid().ToString();
        var jobId = context.JobDetail.Key.Name;
        var jobType = GetJobTypeFromContext(context);
        var domainId = GetDomainIdFromContext(context);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Executing HTTP job: {JobId}, Type: {JobType}, ExecutionId: {ExecutionId}",
            jobId, jobType, executionId);

        JobExecution? execution = null;
        ScheduledJob? job = null;
        string? dgToken = jobType == JobType.User
            ? await GetServiceTokenAsync(context.CancellationToken)
            : null;

        try
        {
            // Get job data from JobDataMap
            var endpointUrl = context.JobDetail.JobDataMap.GetString("EndpointUrl");
            var httpMethod = context.JobDetail.JobDataMap.GetString("HttpMethod") ?? "POST";
            var timeoutSeconds = context.JobDetail.JobDataMap.GetInt("TimeoutSeconds");
            var headersJson = context.JobDetail.JobDataMap.GetString("Headers");
            var payloadJson = context.JobDetail.JobDataMap.GetString("Payload");
            
            // For POST requests, use default payload if not provided
            if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(payloadJson))
            {
                payloadJson = "{}"; // Default empty JSON object
            }

            if (string.IsNullOrEmpty(endpointUrl))
            {
                throw new InvalidOperationException($"EndpointUrl is required for job {jobId}");
            }

            // Load job entity to check ShouldExecute and update execution counts
            job = await LoadJobAsync(jobId, jobType, domainId, dgToken);
            if (job == null)
            {
                throw new InvalidOperationException($"Job {jobId} not found");
            }

            // Check if job should execute (StartDate, ExpireDate, MaxExecutionCount, IsActive)
            if (!job.ShouldExecute())
            {
                _logger.LogWarning("Job {JobId} should not execute (IsActive: {IsActive}, StartDate: {StartDate}, ExpireDate: {ExpireDate}, TotalExecutionCount: {TotalExecutionCount}/{MaxExecutionCount})",
                    jobId, job.IsActive, job.StartDate, job.ExpireDate, job.TotalExecutionCount, job.MaxExecutionCount);
                return; // Job should not execute, skip
            }

            // Create HTTP request
            var httpClient = _httpClientFactory.CreateClient("HttpJob");
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : _settings.HttpClient.TimeoutSeconds);

            // Parse headers
            Dictionary<string, string>? headers = null;
            if (!string.IsNullOrEmpty(headersJson))
            {
                try
                {
                    headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse headers JSON for job {JobId}", jobId);
                }
            }

            // Add headers to HTTP client
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (httpClient.DefaultRequestHeaders.Contains(header.Key))
                    {
                        httpClient.DefaultRequestHeaders.Remove(header.Key);
                    }
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            // Execute HTTP request
            HttpResponseMessage? response = null;
            string? responseBody = null;
            int? responseCode = null;
            string? errorMessage = null;
            string status = "success";

            try
            {
                if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    response = await httpClient.GetAsync(endpointUrl);
                }
                else if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    HttpContent? content = null;
                    if (!string.IsNullOrEmpty(payloadJson))
                    {
                        try
                        {
                            // Try to parse as JSON object
                            var jsonDoc = JsonDocument.Parse(payloadJson);
                            content = JsonContent.Create(jsonDoc);
                        }
                        catch
                        {
                            // If not valid JSON, send as string
                            content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                        }
                    }
                    else
                    {
                        content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                    }

                    response = await httpClient.PostAsync(endpointUrl, content);
                }
                else
                {
                    throw new NotSupportedException($"HTTP method '{httpMethod}' is not supported. Only GET and POST are supported.");
                }

                responseCode = (int)response.StatusCode;
                stopwatch.Stop();

                // Read response body (truncate if too large)
                try
                {
                    responseBody = await response.Content.ReadAsStringAsync();
                    if (responseBody.Length > 10240) // 10KB limit
                    {
                        responseBody = responseBody.Substring(0, 10240) + "... [truncated]";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read response body for job {JobId}", jobId);
                }

                // Determine status based on HTTP status code
                if (!response.IsSuccessStatusCode)
                {
                    status = "failed";
                    errorMessage = $"HTTP {responseCode}: {response.ReasonPhrase}";
                    if (!string.IsNullOrEmpty(responseBody))
                    {
                        errorMessage += $" - {responseBody.Substring(0, Math.Min(200, responseBody.Length))}";
                    }
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                stopwatch.Stop();
                status = "timeout";
                errorMessage = $"Request timeout after {httpClient.Timeout.TotalSeconds} seconds";
                _logger.LogWarning(ex, "HTTP request timeout for job {JobId}", jobId);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                status = "failed";
                errorMessage = ex.Message;
                _logger.LogError(ex, "HTTP request failed for job {JobId}", jobId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                status = "failed";
                errorMessage = ex.Message;
                _logger.LogError(ex, "Unexpected error during HTTP request for job {JobId}", jobId);
            }

            // Create execution record
            execution = new JobExecution
            {
                ExecutionId = executionId,
                JobId = jobId,
                Status = status,
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseCode = responseCode,
                ResponseBody = responseBody,
                ErrorMessage = errorMessage,
                RetryCount = 0,
                DomainId = domainId
            };

            // Save execution history
            if (jobType == JobType.System)
            {
                await _executionRepository.SaveSystemJobExecutionAsync(execution);
            }
            else
            {
                if (string.IsNullOrEmpty(domainId))
                {
                    throw new InvalidOperationException($"DomainId is required for User job {jobId}");
                }
                await _executionRepository.SaveUserJobExecutionAsync(domainId, execution, dgToken);
            }

            // Update job execution counts
            if (status == "success")
            {
                job.IncrementSuccessfulExecutionCount();
            }
            else
            {
                job.IncrementFailedExecutionCount();
            }

            // Check execution limit and auto-deactivate if needed
            var shouldContinue = job.CheckExecutionLimit();
            if (!shouldContinue)
            {
                _logger.LogInformation("Job {JobId} reached execution limit ({TotalExecutionCount}/{MaxExecutionCount}), auto-deactivated",
                    jobId, job.TotalExecutionCount, job.MaxExecutionCount);
            }

            // Update last execution info
            job.LastExecution = execution;
            job.UpdatedAt = DateTime.UtcNow;

            // Update job in repository (job might have been deleted during execution, so handle gracefully)
            try
            {
                if (jobType == JobType.System)
                {
                    await _systemJobRepository.UpdateJobAsync(job);
                }
                else
                {
                    if (string.IsNullOrEmpty(domainId))
                    {
                        throw new InvalidOperationException($"DomainId is required for User job {jobId}");
                    }
                    await _userJobRepository.UpdateJobAsync(domainId, job, dgToken);
                }
            }
            catch (Domain.Exceptions.JobNotFoundException)
            {
                // Job was deleted during execution, log warning but don't fail
                _logger.LogWarning("Job {JobId} was deleted during execution, skipping job update. Execution record was saved.", jobId);
            }

            // Publish RabbitMQ event
            try
            {
                await _eventPublisher.PublishJobExecutionCompletedAsync(execution, job);
            }
            catch (Exception ex)
            {
                // Don't fail job execution if event publishing fails
                _logger.LogWarning(ex, "Failed to publish job execution event for job {JobId}", jobId);
            }

            _logger.LogInformation("HTTP job execution completed: {JobId}, Status: {Status}, ResponseTime: {ResponseTimeMs}ms, ResponseCode: {ResponseCode}",
                jobId, status, stopwatch.ElapsedMilliseconds, responseCode);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing HTTP job {JobId}", jobId);

            // Create failed execution record
            if (execution == null)
            {
                execution = new JobExecution
                {
                    ExecutionId = executionId,
                    JobId = jobId,
                    Status = "failed",
                    ExecutedAt = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = ex.Message,
                    RetryCount = 0,
                    DomainId = domainId
                };

                try
                {
                    if (jobType == JobType.System)
                    {
                        await _executionRepository.SaveSystemJobExecutionAsync(execution);
                    }
                    else if (!string.IsNullOrEmpty(domainId))
                    {
                        await _executionRepository.SaveUserJobExecutionAsync(domainId, execution, dgToken);
                    }
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to save execution record for job {JobId}", jobId);
                }
            }

            // Update job execution counts if job was loaded
            if (job != null)
            {
                try
                {
                    job.IncrementFailedExecutionCount();
                    job.CheckExecutionLimit();
                    job.LastExecution = execution;
                    job.UpdatedAt = DateTime.UtcNow;

                    if (jobType == JobType.System)
                    {
                        await _systemJobRepository.UpdateJobAsync(job);
                    }
                    else if (!string.IsNullOrEmpty(domainId))
                    {
                        await _userJobRepository.UpdateJobAsync(domainId, job, dgToken);
                    }
                }
                catch (Domain.Exceptions.JobNotFoundException)
                {
                    // Job was deleted during execution, log warning but don't fail
                    _logger.LogWarning("Job {JobId} was deleted during execution, skipping job update. Execution record was saved.", jobId);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to update job {JobId} after execution failure", jobId);
                }
            }

            throw; // Re-throw to let Quartz handle retry logic if configured
        }
    }

    private JobType GetJobTypeFromContext(IJobExecutionContext context)
    {
        var jobTypeStr = context.JobDetail.JobDataMap.GetString("JobType");
        if (Enum.TryParse<JobType>(jobTypeStr, out var jobType))
        {
            return jobType;
        }
        return JobType.System; // Default
    }

    private string? GetDomainIdFromContext(IJobExecutionContext context)
    {
        return context.JobDetail.JobDataMap.GetString("DomainId");
    }

    private async Task<ScheduledJob?> LoadJobAsync(string jobId, JobType jobType, string? domainId, string? dgToken = null)
    {
        try
        {
            if (jobType == JobType.System)
            {
                return await _systemJobRepository.GetJobByIdAsync(jobId);
            }

            if (string.IsNullOrEmpty(domainId))
            {
                _logger.LogWarning("DomainId is missing for User job {JobId}", jobId);
                return null;
            }

            return await _userJobRepository.GetJobByIdAsync(domainId, jobId, dgToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load job {JobId} from repository", jobId);
            return null;
        }
    }

    private async Task<string?> GetServiceTokenAsync(CancellationToken cancellationToken)
    {
        var tokenResult = await _keeperAuth.GetAccessTokenAsync(cancellationToken);
        return tokenResult.Success ? tokenResult.AccessToken : null;
    }
}
