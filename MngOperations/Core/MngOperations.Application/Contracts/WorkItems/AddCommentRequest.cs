namespace MngOperations.Application.Contracts.WorkItems;

public sealed class AddCommentRequest
{
    public required string Body { get; init; }
    public string? ParentCommentId { get; init; }

    /// <summary>Yorumda etiketlenen kişi id'leri (mention). op_comments.mentions + in-app bildirim.</summary>
    public IReadOnlyList<string>? Mentions { get; init; }

    /// <summary>Yorum ekleri (yeni dosyalar). DG `op_comments.attachments` (file isArray) inline yüklenir.</summary>
    public IReadOnlyList<CommentAttachmentInput>? Attachments { get; init; }
}

/// <summary>Yeni yorum eki — base64 içerik + orijinal dosya adı (DG MinIO'ya yükler).</summary>
public sealed class CommentAttachmentInput
{
    public required string Content { get; init; }
    public required string OriginalFileName { get; init; }
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
