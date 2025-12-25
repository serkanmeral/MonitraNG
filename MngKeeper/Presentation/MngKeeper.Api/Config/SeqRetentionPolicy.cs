using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Api.Config;

public static class SeqRetentionPolicy
{
    public static async Task ConfigureRetentionPoliciesAsync(IConfiguration configuration, ILogger logger)
    {
        try
        {
            var seqUrl = configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
            var apiKey = configuration["Seq:ApiKey"];

            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(seqUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpClient.DefaultRequestHeaders.Add("X-Seq-ApiKey", apiKey);
            }
            
            // Set Accept header for JSON
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Check if Seq is available
            try
            {
                var healthCheck = await httpClient.GetAsync("/api");
                if (!healthCheck.IsSuccessStatusCode)
                {
                    logger.LogWarning("Seq server is not available. Skipping retention policy configuration.");
                    return;
                }
            }
            catch
            {
                logger.LogWarning("Seq server is not reachable. Skipping retention policy configuration.");
                return;
            }

            // Get existing signals
            // Seq API: /api/signals endpoint (no authentication needed for localhost in development)
            HttpResponseMessage? signalsResponse = null;
            string? signalsJson = null;
            
            try
            {
                // Try the standard Seq API endpoint
                signalsResponse = await httpClient.GetAsync("/api/signals");
                
                if (signalsResponse.IsSuccessStatusCode)
                {
                    signalsJson = await signalsResponse.Content.ReadAsStringAsync();
                    logger.LogDebug("Successfully retrieved Seq signals from /api/signals");
                }
                else
                {
                    // Log the error for debugging
                    var errorContent = await signalsResponse.Content.ReadAsStringAsync();
                    logger.LogWarning("Seq API /api/signals returned status {StatusCode}. Error: {Error}. " +
                        "This might be normal if Seq doesn't support programmatic signal management. " +
                        "You can configure retention policies manually in Seq UI at {SeqUrl}",
                        signalsResponse.StatusCode, 
                        string.IsNullOrEmpty(errorContent) ? "No error details" : errorContent,
                        seqUrl);
                    
                    // If 404 or 401, Seq might not support this endpoint or require authentication
                    // We'll skip automatic configuration but log a helpful message
                    if (signalsResponse.StatusCode == System.Net.HttpStatusCode.NotFound || 
                        signalsResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        logger.LogInformation("Seq retention policy auto-configuration is not available. " +
                            "Please configure retention policies manually in Seq UI: {SeqUrl}/#/settings/retention. " +
                            "Recommended: Information logs = 1 day, Warning/Error logs = 5 days",
                            seqUrl);
                        return;
                    }
                    
                    // For other errors, return
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error connecting to Seq API at {SeqUrl}. " +
                    "Make sure Seq is running. Retention policies can be configured manually in Seq UI.",
                    seqUrl);
                return;
            }
            
            if (string.IsNullOrEmpty(signalsJson))
            {
                logger.LogWarning("Seq signals response was empty. Skipping retention policy configuration.");
                return;
            }

            var signals = JsonSerializer.Deserialize<SeqSignal[]>(signalsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? Array.Empty<SeqSignal>();

            // Create or update Information logs retention policy (1 day)
            await CreateOrUpdateRetentionPolicyAsync(
                httpClient,
                logger,
                signals,
                "Information Logs",
                "Level = 'Information'",
                TimeSpan.FromDays(1));

            // Create or update Warning logs retention policy (5 days)
            await CreateOrUpdateRetentionPolicyAsync(
                httpClient,
                logger,
                signals,
                "Warning Logs",
                "Level = 'Warning'",
                TimeSpan.FromDays(5));

            // Create or update Error logs retention policy (5 days)
            await CreateOrUpdateRetentionPolicyAsync(
                httpClient,
                logger,
                signals,
                "Error Logs",
                "Level = 'Error' or Level = 'Fatal'",
                TimeSpan.FromDays(5));

            logger.LogInformation("Seq retention policies configured successfully");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to configure Seq retention policies. This is not critical and the application will continue.");
        }
    }

    private static async Task CreateOrUpdateRetentionPolicyAsync(
        HttpClient httpClient,
        ILogger logger,
        SeqSignal[] existingSignals,
        string signalTitle,
        string query,
        TimeSpan retentionPeriod)
    {
        try
        {
            // Check if signal already exists
            var existingSignal = existingSignals.FirstOrDefault(s => s.Title == signalTitle);

            // Seq API structure for signals with retention
            var signalData = new
            {
                Title = signalTitle,
                Description = $"Auto-configured retention policy: {retentionPeriod.TotalDays} days",
                Query = query
            };

            string? signalId = null;
            
            if (existingSignal != null)
            {
                // Update existing signal
                var updateResponse = await httpClient.PutAsJsonAsync(
                    $"/api/signals/{existingSignal.Id}",
                    signalData);

                if (updateResponse.IsSuccessStatusCode)
                {
                    signalId = existingSignal.Id;
                    logger.LogDebug("Updated Seq signal: {SignalTitle}", signalTitle);
                }
                else
                {
                    logger.LogWarning("Failed to update Seq signal: {SignalTitle}", signalTitle);
                    return;
                }
            }
            else
            {
                // Create new signal
                var createResponse = await httpClient.PostAsJsonAsync("/api/signals", signalData);

                if (createResponse.IsSuccessStatusCode)
                {
                    var createdSignalJson = await createResponse.Content.ReadAsStringAsync();
                    var createdSignal = JsonSerializer.Deserialize<SeqSignal>(createdSignalJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    signalId = createdSignal?.Id;
                    logger.LogDebug("Created Seq signal: {SignalTitle}", signalTitle);
                }
                else
                {
                    logger.LogWarning("Failed to create Seq signal: {SignalTitle}", signalTitle);
                    return;
                }
            }

            // Set retention policy for the signal
            // Seq API: Signal update with retention policy
            if (!string.IsNullOrEmpty(signalId))
            {
                // Seq API structure: Signal with retention policy
                var signalWithRetention = new
                {
                    Title = signalTitle,
                    Description = $"Auto-configured retention policy: {retentionPeriod.TotalDays} days",
                    Query = query,
                    RetentionPolicy = new
                    {
                        RetentionTime = $"{(int)retentionPeriod.TotalDays}.00:00:00" // Format: "Days.Hours:Minutes:Seconds"
                    }
                };

                var updateWithRetentionResponse = await httpClient.PutAsJsonAsync(
                    $"/api/signals/{signalId}",
                    signalWithRetention);

                if (updateWithRetentionResponse.IsSuccessStatusCode)
                {
                    logger.LogInformation("Set retention policy for {SignalTitle}: {RetentionDays} days", 
                        signalTitle, retentionPeriod.TotalDays);
                }
                else
                {
                    var errorContent = await updateWithRetentionResponse.Content.ReadAsStringAsync();
                    logger.LogWarning("Failed to set retention policy for {SignalTitle}. " +
                        "Error: {Error}. You may need to configure retention policies manually in Seq UI at: {SeqUrl}/#/settings/retention",
                        signalTitle, errorContent, httpClient.BaseAddress);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error configuring retention policy for {SignalTitle}", signalTitle);
        }
    }

    private class SeqSignal
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
    }
}

