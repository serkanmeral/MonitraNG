using MediatR;
using MngLLM.Application.DTOs;

namespace MngLLM.Application.Commands.TranslateText;

/// <summary>
/// Translate text command (CQRS)
/// </summary>
public class TranslateTextCommand : IRequest<TranslationResponseDto>
{
    public string Text { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "tr";
    public List<string> TargetLanguages { get; set; } = new();
}
