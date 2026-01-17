namespace MngLLM.Domain.Interfaces;

/// <summary>
/// Chatbot Service Interface - Moni chatbot için
/// </summary>
public interface IChatbotService
{
    /// <summary>
    /// Process user message and generate response
    /// </summary>
    /// <param name="request">Chat request with message and context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chat response with answer and metadata</returns>
    Task<ChatResponse> ProcessMessageAsync(
        ChatRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear conversation context for a session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task ClearSessionAsync(
        string sessionId, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Chat request model
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Session identifier (for context management)
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// User language preference (tr, en, fr, ar, zh)
    /// </summary>
    public string Language { get; set; } = "tr";
    
    /// <summary>
    /// Conversation history (optional, for context)
    /// </summary>
    public List<ChatMessage>? ConversationHistory { get; set; }
}

/// <summary>
/// Chat response model
/// </summary>
public class ChatResponse
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
    public List<DocumentationSource> DocumentationSources { get; set; } = new();
    
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
/// Chat message (for conversation history)
/// </summary>
public class ChatMessage
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
/// Documentation source reference
/// </summary>
public class DocumentationSource
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
