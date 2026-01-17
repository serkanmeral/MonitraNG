using MediatR;
using MngLLM.Application.DTOs;

namespace MngLLM.Application.Commands.Chat;

/// <summary>
/// Chat command (CQRS) - Process user message
/// </summary>
public class ChatCommand : IRequest<ChatResponseDto>
{
    /// <summary>
    /// User message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Session identifier
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// User language preference
    /// </summary>
    public string Language { get; set; } = "tr";
    
    /// <summary>
    /// Conversation history (optional)
    /// </summary>
    public List<ChatMessageDto>? ConversationHistory { get; set; }
}
