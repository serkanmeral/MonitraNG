namespace MngLLM.Domain.Interfaces;

/// <summary>
/// LLM Service Interface - Provider-agnostic LLM operations
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// Generate text from prompt
    /// </summary>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate text with specific model
    /// </summary>
    Task<string> GenerateAsync(string prompt, string model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate with JSON mode (low temperature). Used for structured extract.
    /// </summary>
    Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate with JSON mode using an explicit model (e.g. faster extract model on CPU).
    /// </summary>
    Task<string> GenerateJsonAsync(string prompt, string model, CancellationToken cancellationToken = default);
}
