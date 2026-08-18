public interface IArticleClient
{
    Task<UpstreamPage<ArticleSummary>> GetByAuthorAsync(
        string authorId, int limit, int offset, CancellationToken ct);
}
