using MediatR;
using Microsoft.Extensions.Logging;
using MngLLM.Application.DTOs;
using MngLLM.Domain.Interfaces;

namespace MngLLM.Application.Commands.TranslateText;

/// <summary>
/// Translate text command handler
/// </summary>
public class TranslateTextCommandHandler : IRequestHandler<TranslateTextCommand, TranslationResponseDto>
{
    private readonly ILLMService _llmService;
    private readonly ILogger<TranslateTextCommandHandler> _logger;
    
    // Language name mapping
    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["tr"] = "Turkish",
        ["en"] = "English",
        ["fr"] = "French",
        ["ar"] = "Arabic",
        ["zh"] = "Chinese"
    };
    
    public TranslateTextCommandHandler(
        ILLMService llmService,
        ILogger<TranslateTextCommandHandler> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<TranslationResponseDto> Handle(TranslateTextCommand request, CancellationToken cancellationToken)
    {
        var translations = new Dictionary<string, string>();
        
        foreach (var targetLang in request.TargetLanguages)
        {
            try
            {
                var prompt = BuildTranslationPrompt(request.Text, request.SourceLanguage, targetLang);
                var translatedText = await _llmService.GenerateAsync(prompt, cancellationToken);
                var cleanedText = CleanTranslationResult(translatedText);
                translations[targetLang] = cleanedText;
                
                _logger.LogDebug(
                    "Translated text from {SourceLang} to {TargetLang}: {Original} -> {Translated}",
                    request.SourceLanguage, targetLang, request.Text, cleanedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate text to {TargetLang}", targetLang);
                // Continue with other languages, use source text as fallback
                translations[targetLang] = request.Text;
            }
        }
        
        return new TranslationResponseDto
        {
            Translations = translations,
            SourceText = request.Text,
            SourceLanguage = request.SourceLanguage
        };
    }
    
    private string BuildTranslationPrompt(string text, string sourceLang, string targetLang)
    {
        var sourceLangName = LanguageNames.GetValueOrDefault(sourceLang, sourceLang);
        var targetLangName = LanguageNames.GetValueOrDefault(targetLang, targetLang);
        
        return $"Translate the following {sourceLangName} text to {targetLangName}. " +
               $"Only return the translation, no explanation:\n\n{text}";
    }
    
    private string CleanTranslationResult(string result)
    {
        // Remove quotes if present
        result = result.Trim().Trim('"').Trim('\'');
        
        // Remove common prefixes like "Translation:", "Result:", etc.
        var prefixes = new[] { "Translation:", "Result:", "Translated:", "Çeviri:" };
        foreach (var prefix in prefixes)
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring(prefix.Length).Trim();
            }
        }
        
        return result.Trim();
    }
}
