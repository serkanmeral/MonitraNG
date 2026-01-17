namespace MngLLM.Domain.Interfaces;

/// <summary>
/// Context Manager Interface - Conversation context yönetimi için
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Get conversation history for a session
    /// </summary>
    List<ChatMessage> GetConversationHistory(string sessionId);
    
    /// <summary>
    /// Add message to conversation history
    /// </summary>
    void AddMessage(string sessionId, ChatMessage message);
    
    /// <summary>
    /// Clear conversation history for a session
    /// </summary>
    void ClearSession(string sessionId);
}
