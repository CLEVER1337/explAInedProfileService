public class ActivityService : IActivityService
{
    private const string ArticlesUpstream = "articles";
    private const string CommentsUpstream = "comments";

    private readonly IArticleClient _articles;
    private readonly ICommentClient _comments;
    private readonly ISubscriptionService _subscriptions;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(
        IArticleClient articles,
        ICommentClient comments,
        ISubscriptionService subscriptions,
        ILogger<ActivityService> logger)
    {
        _articles = articles;
        _comments = comments;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task<ActivityPageDto<ArticleSummary>> GetArticlesAsync(
        string userId, int limit, int offset, CancellationToken ct)
    {
        var page = await TryFetchAsync(ArticlesUpstream, () => _articles.GetByAuthorAsync(userId, limit, offset, ct));

        return new ActivityPageDto<ArticleSummary>(page?.Items ?? [], Degraded: page is null);
    }

    public async Task<ActivityPageDto<CommentSummary>> GetCommentsAsync(
        string userId, int limit, int offset, CancellationToken ct)
    {
        var page = await TryFetchAsync(CommentsUpstream, () => _comments.GetByAuthorAsync(userId, limit, offset, ct));

        return new ActivityPageDto<CommentSummary>(page?.Items ?? [], Degraded: page is null);
    }

    public async Task<ProfileStatsDto> GetStatsAsync(string userId, CancellationToken ct)
    {
        var articles = TryFetchAsync(ArticlesUpstream, () => _articles.GetByAuthorAsync(userId, 1, 0, ct));
        var comments = TryFetchAsync(CommentsUpstream, () => _comments.GetByAuthorAsync(userId, 1, 0, ct));

        await Task.WhenAll(articles, comments);

        return new ProfileStatsDto(
            Articles: (await articles)?.Total,
            Comments: (await comments)?.Total,
            Followers: await _subscriptions.CountFollowersAsync(userId),
            Following: await _subscriptions.CountFollowingAsync(userId));
    }

    private async Task<UpstreamPage<T>?> TryFetchAsync<T>(
        string upstream, Func<Task<UpstreamPage<T>>> fetch)
    {
        try
        {
            return await fetch();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ProfileMetrics.UpstreamFailures.WithLabels(upstream).Inc();
            _logger.LogWarning(ex, "{Upstream} upstream failed; degrading", upstream);

            return null;
        }
    }
}
