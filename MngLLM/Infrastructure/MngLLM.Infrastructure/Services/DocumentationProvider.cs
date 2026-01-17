using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLLM.Application.Configuration;
using MngLLM.Domain.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace MngLLM.Infrastructure.Services;

/// <summary>
/// Documentation Provider Implementation
/// - Markdown dosyalarını parse eder
/// - OpenAPI JSON dosyalarını parse eder (runtime'da HTTP ile)
/// - Keyword index oluşturur
/// - Search algoritması sağlar
/// </summary>
public class DocumentationProvider : IDocumentationProvider
{
    private readonly ILogger<DocumentationProvider> _logger;
    private readonly DocumentationSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    
    // In-memory index
    private readonly Dictionary<string, DocumentationIndex> _index = new();
    private readonly Dictionary<string, List<string>> _keywordIndex = new(); // keyword -> document IDs
    private readonly object _indexLock = new();
    private DateTime _lastIndexTime = DateTime.MinValue;

    public DocumentationProvider(
        ILogger<DocumentationProvider> logger,
        IOptions<MngLLMSettings> settings,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        _logger = logger;
        _settings = settings.Value.Documentation;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _cache = cache;
    }

    /// <summary>
    /// Search documentation by query
    /// </summary>
    public async Task<List<DocumentationResult>> SearchAsync(
        string query, 
        int limit = 5, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<DocumentationResult>();

        // Ensure index is up to date
        await EnsureIndexedAsync(cancellationToken);

        var searchLimit = limit > 0 ? limit : _settings.SearchLimit;
        var queryLower = query.ToLowerInvariant();
        var queryKeywords = ExtractKeywords(queryLower);

        lock (_indexLock)
        {
            var results = new List<DocumentationResult>();

            // Search in index
            foreach (var doc in _index.Values)
            {
                var score = CalculateRelevanceScore(doc, queryLower, queryKeywords);
                if (score > 0)
                {
                    results.Add(new DocumentationResult
                    {
                        Id = doc.Id,
                        Title = doc.Title,
                        Snippet = ExtractSnippet(doc.Content, queryLower, 200),
                        Source = doc.Source,
                        Service = doc.Service,
                        Category = doc.Category,
                        FilePath = doc.FilePath,
                        RelevanceScore = score,
                        Metadata = new Dictionary<string, object>
                        {
                            { "keywords", doc.Keywords },
                            { "lastUpdated", doc.LastUpdated }
                        }
                    });
                }
            }

            // Sort by relevance score (descending)
            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .Take(searchLimit)
                .ToList();

            return results;
        }
    }

    /// <summary>
    /// Get full content of a document
    /// </summary>
    public async Task<string> GetContentAsync(
        string documentId, 
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexedAsync(cancellationToken);

        lock (_indexLock)
        {
            if (_index.TryGetValue(documentId, out var doc))
            {
                return doc.Content;
            }
        }

        // If not in index, try to read from file
        if (File.Exists(documentId))
        {
            return await File.ReadAllTextAsync(documentId, cancellationToken);
        }

        throw new FileNotFoundException($"Document not found: {documentId}");
    }

    /// <summary>
    /// Get all indexed documents
    /// </summary>
    public async Task<List<DocumentationIndex>> GetAllDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexedAsync(cancellationToken);

        lock (_indexLock)
        {
            return _index.Values.ToList();
        }
    }

    /// <summary>
    /// Re-index all documentation
    /// </summary>
    public async Task ReindexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting documentation re-indexing...");

        lock (_indexLock)
        {
            _index.Clear();
            _keywordIndex.Clear();
        }

        await IndexMarkdownFilesAsync(cancellationToken);
        await IndexOpenApiFilesAsync(cancellationToken);

        _lastIndexTime = DateTime.UtcNow;
        _logger.LogInformation("Documentation re-indexing completed. Total documents: {Count}", _index.Count);
    }

    #region Private Methods

    /// <summary>
    /// Ensure index is up to date
    /// </summary>
    private async Task EnsureIndexedAsync(CancellationToken cancellationToken)
    {
        var shouldReindex = _index.Count == 0 || 
                           (DateTime.UtcNow - _lastIndexTime).TotalMinutes >= _settings.ReindexIntervalMinutes;

        if (shouldReindex)
        {
            await ReindexAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Index all markdown files
    /// </summary>
    private async Task IndexMarkdownFilesAsync(CancellationToken cancellationToken)
    {
        // Try multiple path resolution strategies
        string markdownPath = string.Empty;
        var triedPaths = new List<string>();
        
        // Strategy 1: Relative to AppContext.BaseDirectory (for published apps)
        var path1 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _settings.MarkdownPath));
        triedPaths.Add(path1);
        if (Directory.Exists(path1))
        {
            markdownPath = path1;
        }
        else
        {
            // Strategy 2: Relative to current working directory (for dotnet run)
            var path2 = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), _settings.MarkdownPath));
            triedPaths.Add(path2);
            if (Directory.Exists(path2))
            {
                markdownPath = path2;
            }
            else
            {
                // Strategy 3: Try from solution root (for development)
                var currentDir = Directory.GetCurrentDirectory();
                var solutionRoot = currentDir;
                while (!string.IsNullOrEmpty(solutionRoot) && !File.Exists(Path.Combine(solutionRoot, "README.md")))
                {
                    var parent = Directory.GetParent(solutionRoot);
                    if (parent == null) break;
                    solutionRoot = parent.FullName;
                }
                
                if (!string.IsNullOrEmpty(solutionRoot))
                {
                    var path3 = Path.GetFullPath(Path.Combine(solutionRoot, "docs", "content"));
                    triedPaths.Add(path3);
                    if (Directory.Exists(path3))
                    {
                        markdownPath = path3;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(markdownPath) || !Directory.Exists(markdownPath))
        {
            _logger.LogWarning("Markdown path does not exist. Tried paths: {Paths}", string.Join(", ", triedPaths));
            return;
        }

        _logger.LogInformation("Indexing markdown files from: {Path}", markdownPath);

        var markdownFiles = Directory.GetFiles(markdownPath, "*.md", SearchOption.AllDirectories);
        
        foreach (var filePath in markdownFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                var doc = ParseMarkdownFile(filePath, content, markdownPath);
                
                if (doc != null)
                {
                    lock (_indexLock)
                    {
                        _index[doc.Id] = doc;
                        IndexKeywords(doc);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing markdown file: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("Indexed {Count} markdown files", markdownFiles.Length);
    }

    /// <summary>
    /// Index OpenAPI JSON files from services (runtime HTTP requests)
    /// </summary>
    private async Task IndexOpenApiFilesAsync(CancellationToken cancellationToken)
    {
        foreach (var endpoint in _settings.ServiceEndpoints)
        {
            try
            {
                var openApiUrl = $"{endpoint.BaseUrl.TrimEnd('/')}{endpoint.OpenApiPath}";
                _logger.LogInformation("Fetching OpenAPI spec from: {Url}", openApiUrl);

                var response = await _httpClient.GetAsync(openApiUrl, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var docs = ParseOpenApiJson(endpoint.ServiceName, jsonContent, openApiUrl);
                    
                    lock (_indexLock)
                    {
                        foreach (var doc in docs)
                        {
                            _index[doc.Id] = doc;
                            IndexKeywords(doc);
                        }
                    }

                    _logger.LogInformation("Indexed OpenAPI spec for {Service}: {Count} endpoints", 
                        endpoint.ServiceName, docs.Count);
                }
                else
                {
                    _logger.LogWarning("Failed to fetch OpenAPI spec from {Url}: {StatusCode}", 
                        openApiUrl, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing OpenAPI for {Service}: {Error}", 
                    endpoint.ServiceName, ex.Message);
            }
        }
    }

    /// <summary>
    /// Parse markdown file and extract metadata
    /// </summary>
    private DocumentationIndex? ParseMarkdownFile(string filePath, string content, string basePath)
    {
        try
        {
            // Parse front matter (YAML)
            var frontMatter = ExtractFrontMatter(content);
            var markdownContent = RemoveFrontMatter(content);

            // Parse markdown
            var pipeline = new MarkdownPipelineBuilder()
                .UseYamlFrontMatter()
                .Build();
            
            var document = Markdown.Parse(markdownContent, pipeline);
            var plainText = ExtractPlainText(document);

            // Extract metadata from front matter
            var metadata = ParseYamlFrontMatter(frontMatter);
            
            var relativePath = Path.GetRelativePath(basePath, filePath).Replace('\\', '/');
            var docId = $"markdown:{relativePath}";

            var title = metadata.GetValueOrDefault("title")?.ToString() 
                       ?? ExtractTitleFromMarkdown(document) 
                       ?? Path.GetFileNameWithoutExtension(filePath) 
                       ?? "Untitled";

            var service = metadata.GetValueOrDefault("service")?.ToString() ?? "Unknown";
            var category = metadata.GetValueOrDefault("category")?.ToString() ?? "general";
            var tags = metadata.GetValueOrDefault("tags") as List<object> 
                      ?? new List<object>();

            var keywords = new List<string>
            {
                title.ToLowerInvariant(),
                service.ToLowerInvariant(),
                category.ToLowerInvariant()
            };
            
            keywords.AddRange(tags.Select(t => t.ToString()?.ToLowerInvariant() ?? "").Where(k => !string.IsNullOrEmpty(k)));
            keywords.AddRange(ExtractKeywords(plainText));

            return new DocumentationIndex
            {
                Id = docId,
                Title = title,
                Content = plainText,
                Source = "markdown",
                Service = service,
                Category = category,
                FilePath = relativePath,
                Keywords = keywords.Distinct().ToList(),
                LastUpdated = File.GetLastWriteTimeUtc(filePath)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing markdown file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Parse OpenAPI JSON and create documentation indices for each endpoint
    /// </summary>
    private List<DocumentationIndex> ParseOpenApiJson(string serviceName, string jsonContent, string sourceUrl)
    {
        var docs = new List<DocumentationIndex>();

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (!root.TryGetProperty("paths", out var paths))
                return docs;

            foreach (var path in paths.EnumerateObject())
            {
                var pathName = path.Name;

                foreach (var method in path.Value.EnumerateObject())
                {
                    if (!IsHttpMethod(method.Name))
                        continue;

                    var operation = method.Value;
                    var summary = operation.TryGetProperty("summary", out var summaryProp) 
                        ? summaryProp.GetString() ?? "" 
                        : "";
                    var description = operation.TryGetProperty("description", out var descProp) 
                        ? descProp.GetString() ?? "" 
                        : "";

                    var docId = $"openapi:{serviceName}:{pathName}:{method.Name}";
                    var title = $"{method.Name.ToUpper()} {pathName}";
                    var content = $"{summary}\n{description}";

                    // Extract parameters
                    if (operation.TryGetProperty("parameters", out var paramsProp))
                    {
                        var paramDescriptions = paramsProp.EnumerateArray()
                            .Select(p => p.TryGetProperty("name", out var nameProp) 
                                ? $"{nameProp.GetString()}: {GetParameterDescription(p)}" 
                                : "")
                            .Where(d => !string.IsNullOrEmpty(d));
                        content += "\n\nParameters:\n" + string.Join("\n", paramDescriptions);
                    }

                    // Extract request body
                    if (operation.TryGetProperty("requestBody", out var reqBodyProp))
                    {
                        content += "\n\nRequest Body: " + GetRequestBodyDescription(reqBodyProp);
                    }

                    // Extract responses
                    if (operation.TryGetProperty("responses", out var responsesProp))
                    {
                        var responseDescriptions = responsesProp.EnumerateObject()
                            .Select(r => $"{r.Name}: {GetResponseDescription(r.Value)}")
                            .Where(d => !string.IsNullOrEmpty(d));
                        content += "\n\nResponses:\n" + string.Join("\n", responseDescriptions);
                    }

                    var keywords = new List<string>
                    {
                        serviceName.ToLowerInvariant(),
                        method.Name.ToLowerInvariant(),
                        pathName.ToLowerInvariant(),
                        title.ToLowerInvariant()
                    }.Where(k => !string.IsNullOrEmpty(k)).ToList();
                    keywords.AddRange(ExtractKeywords(content));

                    docs.Add(new DocumentationIndex
                    {
                        Id = docId,
                        Title = title,
                        Content = content,
                        Source = "openapi",
                        Service = serviceName,
                        Category = "api",
                        FilePath = sourceUrl,
                        Keywords = keywords.Distinct().ToList(),
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing OpenAPI JSON for {Service}: {Error}", serviceName, ex.Message);
        }

        return docs;
    }

    /// <summary>
    /// Extract front matter (YAML) from markdown
    /// </summary>
    private string ExtractFrontMatter(string content)
    {
        var frontMatterRegex = new Regex(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline | RegexOptions.Multiline);
        var match = frontMatterRegex.Match(content);
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// Remove front matter from markdown content
    /// </summary>
    private string RemoveFrontMatter(string content)
    {
        var frontMatterRegex = new Regex(@"^---\s*\n.*?\n---\s*\n", RegexOptions.Singleline | RegexOptions.Multiline);
        return frontMatterRegex.Replace(content, "");
    }

    /// <summary>
    /// Parse YAML front matter to dictionary
    /// </summary>
    private Dictionary<string, object> ParseYamlFrontMatter(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
            return new Dictionary<string, object>();

        try
        {
            var deserializer = new DeserializerBuilder().Build();
            var result = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            return result ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing YAML front matter");
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Extract plain text from markdown document
    /// </summary>
    private string ExtractPlainText(MarkdownDocument document)
    {
        var text = new System.Text.StringBuilder();
        
        foreach (var block in document)
        {
            if (block is ParagraphBlock paragraph && paragraph.Inline != null)
            {
                foreach (var inline in paragraph.Inline)
                {
                    if (inline is Markdig.Syntax.Inlines.LiteralInline literal)
                    {
                        text.Append(literal.Content.ToString());
                        text.Append(' ');
                    }
                }
            }
            else if (block is HeadingBlock heading && heading.Inline != null)
            {
                foreach (var inline in heading.Inline)
                {
                    if (inline is Markdig.Syntax.Inlines.LiteralInline literal)
                    {
                        text.Append(literal.Content.ToString());
                        text.Append(' ');
                    }
                }
            }
        }

        return text.ToString().Trim();
    }

    /// <summary>
    /// Extract title from markdown document (first H1)
    /// </summary>
    private string? ExtractTitleFromMarkdown(MarkdownDocument document)
    {
        var firstHeading = document.Descendants<HeadingBlock>()
            .FirstOrDefault(h => h.Level == 1);

        if (firstHeading != null)
        {
                    var title = new System.Text.StringBuilder();
                    if (firstHeading.Inline != null)
                    {
                        foreach (var inline in firstHeading.Inline)
                        {
                            if (inline is Markdig.Syntax.Inlines.LiteralInline literal)
                            {
                                title.Append(literal.Content.ToString());
                            }
                        }
                    }
                    return title.ToString().Trim();
        }

        return null;
    }

    /// <summary>
    /// Extract keywords from text
    /// </summary>
    private List<string> ExtractKeywords(string text)
    {
        // Basit keyword extraction (kelimeleri ayır, stop words'leri filtrele)
        var stopWords = new HashSet<string> { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "should", "could", "may", "might", "must", "can", "this", "that", "these", "those", "i", "you", "he", "she", "it", "we", "they", "bir", "ve", "ile", "için", "gibi", "kadar", "daha", "en", "çok", "az", "var", "yok", "olmak", "etmek", "yapmak", "gelmek", "gitmek" };

        var words = Regex.Split(text.ToLowerInvariant(), @"\W+")
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .ToList();

        return words;
    }

    /// <summary>
    /// Index keywords for a document
    /// </summary>
    private void IndexKeywords(DocumentationIndex doc)
    {
        foreach (var keyword in doc.Keywords)
        {
            if (!_keywordIndex.ContainsKey(keyword))
            {
                _keywordIndex[keyword] = new List<string>();
            }

            if (!_keywordIndex[keyword].Contains(doc.Id))
            {
                _keywordIndex[keyword].Add(doc.Id);
            }
        }
    }

    /// <summary>
    /// Calculate relevance score for a document
    /// </summary>
    private double CalculateRelevanceScore(DocumentationIndex doc, string query, List<string> queryKeywords)
    {
        double score = 0.0;

        // Title match (yüksek öncelik)
        if (doc.Title.ToLowerInvariant().Contains(query))
        {
            score += 0.5;
        }

        // Keyword matching
        var matchedKeywords = queryKeywords.Count(k => doc.Keywords.Contains(k));
        if (queryKeywords.Count > 0)
        {
            score += (double)matchedKeywords / queryKeywords.Count * 0.3;
        }

        // Content matching (düşük öncelik)
        if (doc.Content.ToLowerInvariant().Contains(query))
        {
            score += 0.2;
        }

        return Math.Min(score, 1.0);
    }

    /// <summary>
    /// Extract snippet from content
    /// </summary>
    private string ExtractSnippet(string content, string query, int maxLength)
    {
        var queryLower = query.ToLowerInvariant();
        var contentLower = content.ToLowerInvariant();
        
        var index = contentLower.IndexOf(queryLower);
        if (index >= 0)
        {
            var start = Math.Max(0, index - maxLength / 2);
            var end = Math.Min(content.Length, index + query.Length + maxLength / 2);
            var snippet = content.Substring(start, end - start);
            
            if (start > 0) snippet = "..." + snippet;
            if (end < content.Length) snippet = snippet + "...";
            
            return snippet.Trim();
        }

        // Fallback: first N characters
        return content.Length > maxLength 
            ? content.Substring(0, maxLength) + "..." 
            : content;
    }

    /// <summary>
    /// Check if string is HTTP method
    /// </summary>
    private bool IsHttpMethod(string method)
    {
        return method.ToLowerInvariant() is "get" or "post" or "put" or "delete" or "patch";
    }

    /// <summary>
    /// Get parameter description from OpenAPI parameter
    /// </summary>
    private string GetParameterDescription(JsonElement param)
    {
        if (param.TryGetProperty("description", out var descProp))
        {
            return descProp.GetString() ?? "";
        }
        return "";
    }

    /// <summary>
    /// Get request body description
    /// </summary>
    private string GetRequestBodyDescription(JsonElement requestBody)
    {
        if (requestBody.TryGetProperty("description", out var descProp))
        {
            return descProp.GetString() ?? "";
        }
        return "See schema";
    }

    /// <summary>
    /// Get response description
    /// </summary>
    private string GetResponseDescription(JsonElement response)
    {
        if (response.TryGetProperty("description", out var descProp))
        {
            return descProp.GetString() ?? "";
        }
        return "";
    }

    #endregion
}
