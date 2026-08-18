public class ActivityServiceTests
{
    private static ArticleSummary Article(string id) =>
        new(id, $"title {id}", "d", "t", "author-1", DateTime.UtcNow, 100);

    private static CommentSummary Comment(int id) =>
        new(id, "author-1", "article-1", "text", DateTime.UtcNow, DateTime.UtcNow);

    private sealed class Fixture : IDisposable
    {
        public FakeArticleClient Articles { get; } = new();
        public FakeCommentClient Comments { get; } = new();
        public ApplicationDbContext Db { get; }
        public SubscriptionService Subscriptions { get; }
        public ActivityService Service { get; }

        public Fixture()
        {
            Db = TestHelpers.NewDb();
            Subscriptions = new SubscriptionService(Db, TestHelpers.NewProfileService(Db));
            Service = TestHelpers.NewActivityService(Articles, Comments, Subscriptions);
        }

        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task ArticlesPassesThroughAndIsNotDegraded()
    {
        using var f = new Fixture();
        f.Articles.Page = new([Article("a1"), Article("a2")], 2);

        var page = await f.Service.GetArticlesAsync("author-1", 20, 0, default);

        Assert.False(page.Degraded);
        Assert.Equal(["a1", "a2"], page.Items.Select(a => a.Id));
        Assert.Equal("author-1", f.Articles.LastAuthorId);
        Assert.Equal(20, f.Articles.LastLimit);
    }

    [Fact]
    public async Task AnEmptyUpstreamPageIsNotDegraded()
    {
        using var f = new Fixture();

        var page = await f.Service.GetArticlesAsync("author-1", 20, 0, default);

        Assert.False(page.Degraded);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task ArticlesDegradesWhenTheUpstreamFails()
    {
        using var f = new Fixture();
        f.Articles.Fault = new HttpRequestException("article service is down");

        var page = await f.Service.GetArticlesAsync("author-1", 20, 0, default);

        Assert.True(page.Degraded);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task ArticlesDegradesOnTimeout()
    {
        using var f = new Fixture();
        f.Articles.Fault = new TaskCanceledException("timed out");

        var page = await f.Service.GetArticlesAsync("author-1", 20, 0, default);

        Assert.True(page.Degraded);
    }

    [Fact]
    public async Task CommentsDegradeIndependentlyOfArticles()
    {
        using var f = new Fixture();
        f.Articles.Page = new([Article("a1")], 1);
        f.Comments.Fault = new HttpRequestException("comment service is down");

        var articles = await f.Service.GetArticlesAsync("author-1", 20, 0, default);
        var comments = await f.Service.GetCommentsAsync("author-1", 20, 0, default);

        Assert.False(articles.Degraded);
        Assert.True(comments.Degraded);
    }

    [Fact]
    public async Task StatsReportsUpstreamTotalsNotPageLengths()
    {
        using var f = new Fixture();
        f.Articles.Page = new([Article("a1")], 42);
        f.Comments.Page = new([Comment(1)], 7);

        var stats = await f.Service.GetStatsAsync("author-1", default);

        Assert.Equal(42, stats.Articles);
        Assert.Equal(7, stats.Comments);
    }

    [Fact]
    public async Task StatsAsksForTheSmallestPossiblePage()
    {
        using var f = new Fixture();

        await f.Service.GetStatsAsync("author-1", default);

        Assert.Equal(1, f.Articles.LastLimit);
        Assert.Equal(0, f.Articles.LastOffset);
    }

    [Fact]
    public async Task StatsReportsNullRatherThanZeroWhenAnUpstreamIsDown()
    {
        using var f = new Fixture();
        f.Articles.Fault = new HttpRequestException("down");
        f.Comments.Page = new([], 3);

        var stats = await f.Service.GetStatsAsync("author-1", default);

        Assert.Null(stats.Articles);
        Assert.Equal(3, stats.Comments);
    }

    [Fact]
    public async Task StatsStillCountsLocalSubscriptionsWhenBothUpstreamsAreDown()
    {
        using var f = new Fixture();
        f.Articles.Fault = new HttpRequestException("down");
        f.Comments.Fault = new HttpRequestException("down");

        await f.Subscriptions.FollowAsync("someone", "author-1");
        await f.Subscriptions.FollowAsync("author-1", "another");

        var stats = await f.Service.GetStatsAsync("author-1", default);

        Assert.Null(stats.Articles);
        Assert.Null(stats.Comments);
        Assert.Equal(1, stats.Followers);
        Assert.Equal(1, stats.Following);
    }

    [Fact]
    public async Task UnexpectedExceptionsAreNotSwallowed()
    {
        using var f = new Fixture();
        f.Articles.Fault = new InvalidOperationException("a bug, not a network problem");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.GetArticlesAsync("author-1", 20, 0, default));
    }
}
