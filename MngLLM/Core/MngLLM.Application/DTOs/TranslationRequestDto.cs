namespace MngLLM.Application.DTOs;

/// <summary>
/// Translation request DTO
/// </summary>
public class TranslationRequestDto
{
    /// <summary>
    /// Text to translate
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Source language code (e.g., "tr", "en")
    /// </summary>
    public string SourceLanguage { get; set; } = "tr";
    
    /// <summary>
    /// Target language codes (e.g., ["en", "fr", "ar", "zh"])
    /// </summary>
    public List<string> TargetLanguages { get; set; } = new();
}

/// <summary>
/// Translation response DTO
/// </summary>
public class TranslationResponseDto
{
    /// <summary>
    /// Translations dictionary (language code -> translated text)
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new();
    
    /// <summary>
    /// Source text
    /// </summary>
    public string SourceText { get; set; } = string.Empty;
    
    /// <summary>
    /// Source language code
    /// </summary>
    public string SourceLanguage { get; set; } = string.Empty;
}
