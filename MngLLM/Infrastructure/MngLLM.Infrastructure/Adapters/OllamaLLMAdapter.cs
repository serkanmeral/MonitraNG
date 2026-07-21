using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLLM.Application.Configuration;
using MngLLM.Domain.Exceptions;
using MngLLM.Domain.Interfaces;
using System.Text.Json;

namespace MngLLM.Infrastructure.Adapters;

/// <summary>
/// Ollama LLM Adapter - Ollama API implementation
/// </summary>
public class OllamaLLMAdapter : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaLLMAdapter> _logger;
    
    public OllamaLLMAdapter(
        IHttpClientFactory httpClientFactory,
        IOptions<MngLLMSettings> settings,
        ILogger<OllamaLLMAdapter> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(settings.Value.Ollama.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(settings.Value.Ollama.Timeout);
        _settings = settings.Value.Ollama;
        _logger = logger;
    }
    
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return await GenerateAsync(prompt, _settings.DefaultModel, jsonMode: false, cancellationToken);
    }
    
    public async Task<string> GenerateAsync(string prompt, string model, CancellationToken cancellationToken = default)
    {
        return await GenerateAsync(prompt, model, jsonMode: false, cancellationToken);
    }

    public async Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return await GenerateAsync(prompt, _settings.DefaultModel, jsonMode: true, cancellationToken);
    }

    public async Task<string> GenerateJsonAsync(string prompt, string model, CancellationToken cancellationToken = default)
    {
        return await GenerateAsync(prompt, model, jsonMode: true, cancellationToken);
    }

    private async Task<string> GenerateAsync(string prompt, string model, bool jsonMode, CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(0, _settings.MaxRetries);
        const int baseDelayMs = 1000; // 1 second
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                object request = jsonMode
                    ? new
                    {
                        model = model,
                        prompt = prompt,
                        stream = false,
                        format = "json",
                        options = new
                        {
                            temperature = 0.1,
                            top_p = 0.9,
                            top_k = 40,
                            num_predict = 768
                        }
                    }
                    : new
                    {
                        model = model,
                        prompt = prompt,
                        stream = false,
                        options = new
                        {
                            temperature = 0.7,
                            top_p = 0.9,
                            top_k = 40
                        }
                    };
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                if (attempt > 0)
                {
                    _logger.LogInformation("Retrying Ollama API call: Attempt {Attempt}/{MaxRetries}", attempt + 1, maxRetries + 1);
                }
                else
                {
                    _logger.LogDebug(
                        "Calling Ollama API: Model={Model}, PromptLength={PromptLength}, JsonMode={JsonMode}",
                        model, prompt.Length, jsonMode);
                }
                
                var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Ollama API error: Status={Status}, Error={Error}", response.StatusCode, errorContent);
                    
                    // Don't retry on client errors (4xx)
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    {
                        throw new LLMServiceException($"Ollama API error: {response.StatusCode} - {errorContent}");
                    }
                    
                    // Retry on server errors (5xx)
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt), cancellationToken);
                        continue;
                    }
                    
                    throw new LLMServiceException($"Ollama API error: {response.StatusCode} - {errorContent}");
                }
                
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<OllamaResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (result?.Response == null)
                {
                    throw new LLMServiceException("Ollama API returned empty response");
                }
                
                _logger.LogDebug("Ollama API response received: Length={Length}", result.Response.Length);
                
                return result.Response;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error calling Ollama API (Attempt {Attempt})", attempt + 1);
                
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt), cancellationToken);
                    continue;
                }
                
                _logger.LogError(ex, "HTTP error calling Ollama API after {MaxRetries} retries", maxRetries + 1);
                throw new LLMServiceException("Failed to connect to Ollama service", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Timeout calling Ollama API (Attempt {Attempt})", attempt + 1);
                
                // Don't retry on timeout - it's likely a real performance issue
                _logger.LogError(ex, "Timeout calling Ollama API");
                throw new LLMServiceException("Ollama service timeout", ex);
            }
            catch (Exception ex) when (!(ex is LLMServiceException))
            {
                _logger.LogWarning(ex, "Unexpected error calling Ollama API (Attempt {Attempt})", attempt + 1);
                
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt), cancellationToken);
                    continue;
                }
                
                _logger.LogError(ex, "Unexpected error calling Ollama API after {MaxRetries} retries", maxRetries + 1);
                throw new LLMServiceException("Unexpected error calling Ollama service", ex);
            }
        }
        
        // Should never reach here, but compiler requires it
        throw new LLMServiceException("Failed to generate response after retries");
    }
    
    private class OllamaResponse
    {
        public string? Response { get; set; }
        public bool Done { get; set; }
    }
}
