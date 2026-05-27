namespace MngOperations.Application.Contracts.WorkItems;

public sealed class AddCommentRequest
{
    public required string Body { get; init; }
    public string? ParentCommentId { get; init; }
}

public sealed class CommentDto
{
    public required string Id { get; init; }
    public required string WorkItemId { get; init; }
    public required string Body { get; init; }
    public string? Author { get; init; }
    public string? ParentCommentId { get; init; }
    public DateTime? CommentDate { get; init; }
}
