public sealed class CommentHttpClient : ICommentClient
{
    private readonly HttpClient _http;

    public CommentHttpClient(HttpClient http) => _http = http;

    public Task<UpstreamPage<CommentSummary>> GetByAuthorAsync(
        string authorId, int limit, int offset, CancellationToken ct) =>
        UpstreamJson.ReadPageAsync<CommentSummary>(
            _http,
            "comment service",
            $"comments/author/{Uri.EscapeDataString(authorId)}?limit={limit}&offset={offset}",
            ct);
}
