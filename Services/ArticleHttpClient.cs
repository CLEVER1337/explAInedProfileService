public sealed class ArticleHttpClient : IArticleClient
{
    private readonly HttpClient _http;

    public ArticleHttpClient(HttpClient http) => _http = http;

    public Task<UpstreamPage<ArticleSummary>> GetByAuthorAsync(
        string authorId, int limit, int offset, CancellationToken ct) =>
        UpstreamJson.ReadPageAsync<ArticleSummary>(
            _http,
            "article service",
            $"articles/by-author/{Uri.EscapeDataString(authorId)}?limit={limit}&offset={offset}",
            ct);
}
