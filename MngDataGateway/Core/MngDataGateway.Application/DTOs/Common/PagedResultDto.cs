namespace MngDataGateway.Application.DTOs.Common;

/// <summary>
/// Generic paged result wrapper
/// </summary>
public class PagedResultDto<T>
{
    /// <summary>
    /// Data items for current page
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total count of items (all pages)
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Page size
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>
    /// Has previous page
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Has next page
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
}

