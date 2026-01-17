namespace MngLLM.Domain.Interfaces;

/// <summary>
/// Documentation Provider Interface - Dokümantasyon arama ve erişim için
/// </summary>
public interface IDocumentationProvider
{
    /// <summary>
    /// Search documentation by query
    /// </summary>
    /// <param name="query">Arama sorgusu</param>
    /// <param name="limit">Maksimum sonuç sayısı (varsayılan: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Documentation sonuçları listesi</returns>
    Task<List<DocumentationResult>> SearchAsync(
        string query, 
        int limit = 5, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get full content of a document
    /// </summary>
    /// <param name="documentId">Dokümantasyon ID (file path veya unique identifier)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dokümantasyon içeriği (markdown formatında)</returns>
    Task<string> GetContentAsync(
        string documentId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all indexed documents
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tüm indekslenmiş dokümantasyonların listesi</returns>
    Task<List<DocumentationIndex>> GetAllDocumentsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Re-index all documentation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    Task ReindexAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Documentation search result
/// </summary>
public class DocumentationResult
{
    /// <summary>
    /// Unique document identifier (file path veya unique ID)
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Document title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Content snippet (özet/kısa içerik)
    /// </summary>
    public string Snippet { get; set; } = string.Empty;
    
    /// <summary>
    /// Source type: "markdown" | "openapi"
    /// </summary>
    public string Source { get; set; } = "markdown";
    
    /// <summary>
    /// Service name: "MngDataGateway", "MngKeeper", "Mng.Ui", etc.
    /// </summary>
    public string Service { get; set; } = string.Empty;
    
    /// <summary>
    /// Category: "api", "architecture", "guide", "ui-guides", "datasets", etc.
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// File path (relative to docs/content)
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Relevance score (0-1 arası, yüksek = daha ilgili)
    /// </summary>
    public double RelevanceScore { get; set; }
    
    /// <summary>
    /// Additional metadata (front matter'dan gelen)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Documentation index entry
/// </summary>
public class DocumentationIndex
{
    /// <summary>
    /// Unique document identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Document title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Full content (index için)
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Source type: "markdown" | "openapi"
    /// </summary>
    public string Source { get; set; } = "markdown";
    
    /// <summary>
    /// Service name
    /// </summary>
    public string Service { get; set; } = string.Empty;
    
    /// <summary>
    /// Category
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// File path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Extracted keywords for search
    /// </summary>
    public List<string> Keywords { get; set; } = new();
    
    /// <summary>
    /// Last update time
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
