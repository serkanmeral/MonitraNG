namespace MngLLM.Application.DTOs;

/// <summary>
/// Chat response DTO
/// </summary>
public class ChatResponseDto
{
    /// <summary>
    /// Bot response message
    /// </summary>
    public string Answer { get; set; } = string.Empty;
    
    /// <summary>
    /// Detected intent (nlq, docs, guide, general)
    /// </summary>
    public string Intent { get; set; } = "general";
    
    /// <summary>
    /// Intent confidence score (0-1)
    /// </summary>
    public double IntentConfidence { get; set; }
    
    /// <summary>
    /// Documentation sources used (if any)
    /// </summary>
    public List<DocumentationSourceDto> DocumentationSources { get; set; } = new();
    
    /// <summary>
    /// Session identifier
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Response metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Chat message DTO (for conversation history)
/// </summary>
public class ChatMessageDto
{
    /// <summary>
    /// Message role: "user" or "assistant"
    /// </summary>
    public string Role { get; set; } = "user";
    
    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Documentation source DTO
/// </summary>
public class DocumentationSourceDto
{
    /// <summary>
    /// Document title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Document ID
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service name
    /// </summary>
    public string Service { get; set; } = string.Empty;
    
    /// <summary>
    /// Category
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Relevance score
    /// </summary>
    public double RelevanceScore { get; set; }
}
