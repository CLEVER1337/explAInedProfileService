public sealed record ArticleSummary(
    string Id,
    string Title,
    string Description,
    string Tags,
    string AuthorId,
    DateTime PublishedAt,
    int WordCount);
