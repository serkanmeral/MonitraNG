namespace MngDocument.Application.Contracts.Tags;

public sealed class TagListResult
{
    public IReadOnlyList<TagDto> Items { get; init; } = Array.Empty<TagDto>();
    public long Total { get; init; }
}

public sealed class TagDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
