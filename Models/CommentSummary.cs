public sealed record CommentSummary(
    int Id,
    string AuthorId,
    string ArticleId,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);
