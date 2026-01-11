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
}
