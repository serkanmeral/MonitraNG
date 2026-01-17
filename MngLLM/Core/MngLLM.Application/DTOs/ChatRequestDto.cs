using System.ComponentModel.DataAnnotations;

namespace MngLLM.Application.DTOs;

/// <summary>
/// Chat request DTO
/// </summary>
public class ChatRequestDto
{
    /// <summary>
    /// User message (required)
    /// </summary>
    [Required(ErrorMessage = "Message is required")]
    [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters")]
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Session identifier (optional, auto-generated if not provided)
    /// </summary>
    [StringLength(100, ErrorMessage = "SessionId cannot exceed 100 characters")]
    public string? SessionId { get; set; }
    
    /// <summary>
    /// User language preference (tr, en, fr, ar, zh)
    /// </summary>
    [RegularExpression("^(tr|en|fr|ar|zh)$", ErrorMessage = "Language must be one of: tr, en, fr, ar, zh")]
    public string Language { get; set; } = "tr";
    
    /// <summary>
    /// Conversation history (optional, for context)
    /// </summary>
    public List<ChatMessageDto>? ConversationHistory { get; set; }
}
