using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MngLLM.Domain.Interfaces;

namespace MngLLM.Infrastructure.Services;

/// <summary>
/// Context Manager - Conversation context yönetimi için
/// </summary>
public class ContextManager : IContextManager
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ContextManager> _logger;
    private const int MaxMessagesPerSession = 10;
    private const int SessionTTLMinutes = 30;

    public ContextManager(
        IMemoryCache cache,
        ILogger<ContextManager> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get conversation history for a session
    /// </summary>
    public List<ChatMessage> GetConversationHistory(string sessionId)
    {
        var cacheKey = GetCacheKey(sessionId);
        if (_cache.TryGetValue(cacheKey, out List<ChatMessage>? history))
        {
            return history ?? new List<ChatMessage>();
        }
        return new List<ChatMessage>();
    }

    /// <summary>
    /// Add message to conversation history
    /// </summary>
    public void AddMessage(string sessionId, ChatMessage message)
    {
        var cacheKey = GetCacheKey(sessionId);
        var history = GetConversationHistory(sessionId);
        
        history.Add(message);
        
        // Keep only last N messages
        if (history.Count > MaxMessagesPerSession)
        {
            history = history.Skip(history.Count - MaxMessagesPerSession).ToList();
        }
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(SessionTTLMinutes),
            SlidingExpiration = TimeSpan.FromMinutes(SessionTTLMinutes)
        };
        
        _cache.Set(cacheKey, history, cacheOptions);
    }

    /// <summary>
    /// Clear conversation history for a session
    /// </summary>
    public void ClearSession(string sessionId)
    {
        var cacheKey = GetCacheKey(sessionId);
        _cache.Remove(cacheKey);
        _logger.LogInformation("Cleared conversation context for session: {SessionId}", sessionId);
    }

    /// <summary>
    /// Generate cache key
    /// </summary>
    private string GetCacheKey(string sessionId)
    {
        return $"chatbot:session:{sessionId}";
    }
}
