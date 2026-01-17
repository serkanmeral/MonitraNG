using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MngLLM.Application.DTOs;
using MngLLM.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace MngLLM.Application.Commands.Chat;

/// <summary>
/// Chat command handler - Process user message and generate response
/// </summary>
public class ChatCommandHandler : IRequestHandler<ChatCommand, ChatResponseDto>
{
    private readonly ILLMService _llmService;
    private readonly IDocumentationProvider _documentationProvider;
    private readonly IContextManager _contextManager;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ChatCommandHandler> _logger;
    private const int CacheTTLHours = 1;

    public ChatCommandHandler(
        ILLMService llmService,
        IDocumentationProvider documentationProvider,
        IContextManager contextManager,
        IMemoryCache cache,
        ILogger<ChatCommandHandler> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _documentationProvider = documentationProvider ?? throw new ArgumentNullException(nameof(documentationProvider));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatResponseDto> Handle(ChatCommand request, CancellationToken cancellationToken)
    {
        // Generate session ID if not provided
        var sessionId = string.IsNullOrEmpty(request.SessionId) 
            ? Guid.NewGuid().ToString() 
            : request.SessionId;

        // Get conversation history
        var conversationHistory = _contextManager.GetConversationHistory(sessionId);
        
        // Add user message to history
        var userMessage = new ChatMessage
        {
            Role = "user",
            Content = request.Message,
            Timestamp = DateTime.UtcNow
        };
        _contextManager.AddMessage(sessionId, userMessage);

        // Step 1: Detect intent
        var intent = await DetectIntentAsync(request.Message, request.Language, cancellationToken);
        _logger.LogInformation("Detected intent: {Intent} (confidence: {Confidence}) for message: {Message}", 
            intent.Intent, intent.Confidence, request.Message);

        // Step 2: Search documentation if needed
        var documentationSources = new List<DocumentationSourceDto>();
        var documentationSnippets = new List<string>();
        if (intent.Intent == "docs" || intent.Intent == "guide" || intent.Intent == "nlq")
        {
            var searchResults = await _documentationProvider.SearchAsync(request.Message, limit: 3, cancellationToken);
            documentationSources = searchResults.Select(r => new DocumentationSourceDto
            {
                Title = r.Title,
                DocumentId = r.Id,
                Service = r.Service,
                Category = r.Category,
                RelevanceScore = r.RelevanceScore
            }).ToList();
            
            // Get snippets for better context
            foreach (var result in searchResults.Take(2))
            {
                if (!string.IsNullOrWhiteSpace(result.Snippet))
                {
                    documentationSnippets.Add($"{result.Title}: {result.Snippet}");
                }
            }
        }

        // Step 3: Check cache first
        var cacheKey = GenerateCacheKey(request.Message, intent.Intent, request.Language);
        if (_cache.TryGetValue(cacheKey, out string? cachedResponse) && !string.IsNullOrEmpty(cachedResponse))
        {
            _logger.LogDebug("Cache hit for message: {Message}", request.Message);
            return new ChatResponseDto
            {
                Answer = cachedResponse,
                Intent = intent.Intent,
                IntentConfidence = intent.Confidence,
                DocumentationSources = documentationSources,
                SessionId = sessionId,
                Metadata = new Dictionary<string, object>
                {
                    { "timestamp", DateTime.UtcNow },
                    { "language", request.Language },
                    { "cached", true }
                }
            };
        }

        // Step 4: Build optimized prompt with context
        var prompt = BuildOptimizedPrompt(request.Message, intent.Intent, documentationSources, documentationSnippets, conversationHistory, request.Language);

        // Step 5: Generate response
        string cleanedResponse;
        try
        {
            var response = await _llmService.GenerateAsync(prompt, cancellationToken);
            cleanedResponse = CleanResponse(response);
            
            // Cache the response
            _cache.Set(cacheKey, cleanedResponse, TimeSpan.FromHours(CacheTTLHours));
            _logger.LogDebug("Response cached for message: {Message}", request.Message);
        }
        catch (Domain.Exceptions.LLMServiceException ex) when (ex.Message.Contains("timeout"))
        {
            _logger.LogWarning(ex, "LLM timeout, providing fallback response");
            cleanedResponse = BuildFallbackResponse(request.Message, intent.Intent, documentationSources, documentationSnippets, request.Language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM response, providing fallback");
            cleanedResponse = BuildFallbackResponse(request.Message, intent.Intent, documentationSources, documentationSnippets, request.Language);
        }

        // Step 6: Add assistant message to history
        var assistantMessage = new ChatMessage
        {
            Role = "assistant",
            Content = cleanedResponse,
            Timestamp = DateTime.UtcNow
        };
        _contextManager.AddMessage(sessionId, assistantMessage);

        return new ChatResponseDto
        {
            Answer = cleanedResponse,
            Intent = intent.Intent,
            IntentConfidence = intent.Confidence,
            DocumentationSources = documentationSources,
            SessionId = sessionId,
            Metadata = new Dictionary<string, object>
            {
                { "timestamp", DateTime.UtcNow },
                { "language", request.Language }
            }
        };
    }

    /// <summary>
    /// Detect intent from user message
    /// </summary>
    private async Task<(string Intent, double Confidence)> DetectIntentAsync(
        string message, 
        string language, 
        CancellationToken cancellationToken)
    {
        // First try keyword-based detection (fast, no LLM call)
        var keywordIntent = DetectIntentByKeywords(message);
        if (keywordIntent.Confidence >= 0.8)
        {
            _logger.LogDebug("Intent detected by keywords: {Intent} (Confidence: {Confidence})", 
                keywordIntent.Intent, keywordIntent.Confidence);
            return keywordIntent;
        }

        // Fallback to LLM-based detection
        var intentPrompt = BuildIntentDetectionPrompt(message, language);
        
        try
        {
            // Use shorter timeout for intent detection (10 seconds)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            var response = await _llmService.GenerateAsync(intentPrompt, cts.Token);
            var intent = ParseIntentResponse(response);
            return intent;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Intent detection timeout, using keyword-based fallback");
            return keywordIntent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting intent with LLM, using keyword-based fallback");
            return keywordIntent;
        }
    }

    /// <summary>
    /// Detect intent using keyword matching (fast, no LLM call)
    /// </summary>
    private (string Intent, double Confidence) DetectIntentByKeywords(string message)
    {
        var messageLower = message.ToLowerInvariant();
        
        // NLQ keywords
        var nlqKeywords = new[] { "göster", "listele", "getir", "bul", "sorgula", "query", "show", "list", "find", "get", "dataset", "veri", "data" };
        var nlqCount = nlqKeywords.Count(k => messageLower.Contains(k));
        if (nlqCount >= 2)
        {
            return ("nlq", 0.85);
        }
        
        // Docs keywords
        var docsKeywords = new[] { "dokümantasyon", "doküman", "api", "documentation", "swagger", "endpoint", "reference" };
        var docsCount = docsKeywords.Count(k => messageLower.Contains(k));
        if (docsCount >= 1)
        {
            return ("docs", 0.8);
        }
        
        // Guide keywords
        var guideKeywords = new[] { "nasıl", "adım", "rehber", "tutorial", "how", "step", "guide", "oluştur", "create", "ekle", "add", "yap", "do", "make" };
        var guideCount = guideKeywords.Count(k => messageLower.Contains(k));
        if (guideCount >= 1)
        {
            return ("guide", 0.75);
        }
        
        // Default to general
        return ("general", 0.5);
    }

    /// <summary>
    /// Build intent detection prompt
    /// </summary>
    private string BuildIntentDetectionPrompt(string message, string language)
    {
        var langName = language switch
        {
            "tr" => "Türkçe",
            "en" => "English",
            "fr" => "Français",
            "ar" => "العربية",
            "zh" => "中文",
            _ => "Türkçe"
        };

        return $@"Analyze the following {langName} message and determine the user's intent. 
Respond with ONLY one word from this list: nlq, docs, guide, general

Intent definitions:
- nlq: Natural Language Query - User wants to query data/datasets (e.g., ""show me users"", ""list datasets"", ""find data"")
- docs: Documentation search - User wants to find documentation or API information (e.g., ""how to use API"", ""documentation"", ""API reference"")
- guide: UI guide or tutorial - User wants step-by-step instructions (e.g., ""how to create user"", ""tutorial"", ""guide"")
- general: General question or conversation (e.g., ""hello"", ""what can you do"", ""help"")

Message: {message}

Intent:";
    }

    /// <summary>
    /// Parse intent from LLM response
    /// </summary>
    private (string Intent, double Confidence) ParseIntentResponse(string response)
    {
        var responseLower = response.Trim().ToLowerInvariant();
        
        // Extract intent keyword
        var intents = new[] { "nlq", "docs", "guide", "general" };
        foreach (var intent in intents)
        {
            if (responseLower.Contains(intent))
            {
                // Simple confidence: if exact match, high confidence; otherwise medium
                var confidence = responseLower.Trim() == intent ? 0.9 : 0.7;
                return (intent, confidence);
            }
        }
        
        // Default fallback
        return ("general", 0.5);
    }

    /// <summary>
    /// Build optimized prompt for LLM with better context
    /// </summary>
    private string BuildOptimizedPrompt(
        string userMessage,
        string intent,
        List<DocumentationSourceDto> documentationSources,
        List<string> documentationSnippets,
        List<ChatMessage> conversationHistory,
        string language)
    {
        var langName = language switch
        {
            "tr" => "Türkçe",
            "en" => "English",
            "fr" => "Français",
            "ar" => "العربية",
            "zh" => "中文",
            _ => "Türkçe"
        };

        // Enhanced system prompt with clearer instructions
        var systemPrompt = language == "tr"
            ? $"Sen Moni, MonitraNG platformunun yardımcı chatbot'usun. Görevlerin:\n" +
              $"- Kullanıcılara platform hakkında bilgi vermek\n" +
              $"- Dokümantasyon ve API'ler hakkında yardımcı olmak\n" +
              $"- Dataset sorguları için rehberlik etmek\n" +
              $"- UI kullanımı hakkında adım adım talimat vermek\n\n" +
              $"Yanıtların {langName} dilinde, kısa, net ve tutarlı olsun. Emin olmadığın konularda dokümantasyon kaynaklarına yönlendir."
            : $"You are Moni, the assistant chatbot for MonitraNG platform. Your tasks:\n" +
              $"- Provide information about the platform\n" +
              $"- Help with documentation and APIs\n" +
              $"- Guide users on dataset queries\n" +
              $"- Provide step-by-step UI instructions\n\n" +
              $"Your answers should be in {langName}, short, clear, and consistent. When unsure, direct users to documentation sources.";

        // Context section (last 3 messages)
        var contextSection = "";
        if (conversationHistory.Count > 0)
        {
            var recentHistory = conversationHistory.TakeLast(3).ToList();
            contextSection = language == "tr" ? "\n\nÖnceki konuşma:\n" : "\n\nPrevious conversation:\n";
            foreach (var msg in recentHistory)
            {
                var roleLabel = msg.Role == "user" 
                    ? (language == "tr" ? "Kullanıcı" : "User")
                    : (language == "tr" ? "Moni" : "Moni");
                contextSection += $"{roleLabel}: {msg.Content}\n";
            }
        }

        // Enhanced docs section with snippets
        var docsSection = "";
        if (documentationSnippets.Count > 0)
        {
            var snippetsText = string.Join("\n\n", documentationSnippets);
            docsSection = language == "tr"
                ? $"\n\nİlgili dokümantasyon bilgileri:\n{snippetsText}"
                : $"\n\nRelevant documentation:\n{snippetsText}";
        }
        else if (documentationSources.Count > 0)
        {
            var docsList = string.Join(", ", documentationSources.Take(3).Select(d => d.Title));
            docsSection = language == "tr"
                ? $"\n\nİlgili kaynaklar: {docsList}"
                : $"\n\nRelevant sources: {docsList}";
        }

        // Enhanced intent section with clearer instructions
        var intentSection = intent switch
        {
            "nlq" => language == "tr" 
                ? "\n\nKullanıcı veri sorgusu istiyor. Dataset query için örnekler ve API endpoint bilgileri ver. Eğer spesifik bir dataset sorusuysa, ilgili dataset API'sini açıkla."
                : "\n\nUser wants data query. Provide dataset query examples and API endpoint information. If it's a specific dataset question, explain the relevant dataset API.",
            "docs" => language == "tr"
                ? "\n\nKullanıcı dokümantasyon arıyor. Yukarıdaki dokümantasyon bilgilerini kullanarak detaylı ve doğru yanıt ver. Eğer yeterli bilgi yoksa, hangi dokümantasyonu kontrol etmesi gerektiğini söyle."
                : "\n\nUser wants documentation. Use the documentation information above to provide detailed and accurate answers. If information is insufficient, tell them which documentation to check.",
            "guide" => language == "tr"
                ? "\n\nKullanıcı rehber istiyor. Adım adım, net talimatlar ver. UI elementlerini ve butonları açıkça belirt."
                : "\n\nUser wants guide. Provide step-by-step, clear instructions. Clearly specify UI elements and buttons.",
            _ => language == "tr"
                ? "\n\nGenel bir soru. Kısa, net ve yardımcı bir yanıt ver."
                : "\n\nGeneral question. Provide a short, clear, and helpful answer."
        };

        return $"{systemPrompt}{contextSection}{docsSection}{intentSection}\n\nKullanıcı: {userMessage}\n\nMoni:";
    }

    /// <summary>
    /// Generate cache key for response caching
    /// </summary>
    private string GenerateCacheKey(string message, string intent, string language)
    {
        // Normalize message (lowercase, trim)
        var normalizedMessage = message.ToLowerInvariant().Trim();
        
        // Create hash for cache key
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{normalizedMessage}:{intent}:{language}"));
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        return $"chatbot:response:{hashString}";
    }

    /// <summary>
    /// Clean LLM response
    /// </summary>
    private string CleanResponse(string response)
    {
        // Remove common prefixes
        var prefixes = new[] { "Moni:", "Assistant:", "Bot:", "Response:" };
        foreach (var prefix in prefixes)
        {
            if (response.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                response = response.Substring(prefix.Length).Trim();
            }
        }

        return response.Trim();
    }

    /// <summary>
    /// Build fallback response when LLM fails
    /// </summary>
    private string BuildFallbackResponse(
        string userMessage,
        string intent,
        List<DocumentationSourceDto> documentationSources,
        List<string> documentationSnippets,
        string language)
    {
        var langName = language switch
        {
            "tr" => "Türkçe",
            "en" => "English",
            "fr" => "Français",
            "ar" => "العربية",
            "zh" => "中文",
            _ => "Türkçe"
        };

        if (documentationSources.Count > 0)
        {
            var sourcesList = string.Join("\n", documentationSources.Select(d => $"- {d.Title} ({d.Service})"));
            
            return language == "tr"
                ? $"Üzgünüm, şu anda yanıt üretemiyorum. Ancak size yardımcı olabilecek dokümantasyon kaynakları buldum:\n\n{sourcesList}\n\nBu kaynaklara bakarak sorunuzu çözebilirsiniz."
                : $"I'm sorry, I cannot generate a response right now. However, I found these documentation sources that might help:\n\n{sourcesList}\n\nYou can check these sources to find the answer.";
        }

        return language == "tr"
            ? "Üzgünüm, şu anda yanıt üretemiyorum. Lütfen daha sonra tekrar deneyin veya dokümantasyonu kontrol edin."
            : "I'm sorry, I cannot generate a response right now. Please try again later or check the documentation.";
    }
}
