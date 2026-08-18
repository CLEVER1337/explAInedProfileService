using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

public class ActivityEndpointsTests : IClassFixture<ProfileWebApplicationFactory>
{
    private const string UserId = "activity-user";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ProfileWebApplicationFactory _factory;

    public ActivityEndpointsTests(ProfileWebApplicationFactory factory)
    {
        _factory = factory;
        ResetState();
        _factory.Articles.Reset();
        _factory.Comments.Reset();
    }

    private void ResetState()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Subscriptions.RemoveRange(db.Subscriptions);
        db.ProfileAvatars.RemoveRange(db.ProfileAvatars);
        db.Profiles.RemoveRange(db.Profiles);
        db.SaveChanges();
    }

    private static ArticleSummary Article(string id) =>
        new(id, $"title {id}", "description", "tags", UserId, DateTime.UtcNow, 500);

    private static CommentSummary Comment(int id) =>
        new(id, UserId, "article-1", "text", DateTime.UtcNow, DateTime.UtcNow);

    private sealed record Page<T>(List<T> Items, bool Degraded);

    [Fact]
    public async Task ArticlesAreServedFromTheUpstream()
    {
        _factory.Articles.Page = new([Article("a1"), Article("a2")], 2);

        var page = await _factory.CreateClient()
            .GetFromJsonAsync<Page<ArticleSummary>>($"/profiles/{UserId}/articles", Json);

        Assert.False(page!.Degraded);
        Assert.Equal(["a1", "a2"], page.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task ArticlesIsPublic()
    {
        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}/articles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ArticlesPassesPagingThroughToTheUpstream()
    {
        await _factory.CreateClient().GetAsync($"/profiles/{UserId}/articles?limit=7&offset=14");

        Assert.Equal(7, _factory.Articles.LastLimit);
        Assert.Equal(14, _factory.Articles.LastOffset);
    }

    [Fact]
    public async Task ArticlesClampsAnAbsurdLimitBeforeCallingTheUpstream()
    {
        await _factory.CreateClient().GetAsync($"/profiles/{UserId}/articles?limit=100000");

        Assert.Equal(100, _factory.Articles.LastLimit);
    }

    [Fact]
    public async Task ArticlesAnswers200WithADegradedFlagWhenTheUpstreamIsDown()
    {
        _factory.Articles.Fault = new HttpRequestException("article service is down");

        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}/articles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<Page<ArticleSummary>>(Json);
        Assert.True(page!.Degraded);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task AnEmptyPageIsDistinguishableFromAFailedOne()
    {
        var healthy = await _factory.CreateClient()
            .GetFromJsonAsync<Page<ArticleSummary>>($"/profiles/{UserId}/articles", Json);

        _factory.Articles.Fault = new HttpRequestException("down");

        var broken = await _factory.CreateClient()
            .GetFromJsonAsync<Page<ArticleSummary>>($"/profiles/{UserId}/articles", Json);

        Assert.Empty(healthy!.Items);
        Assert.Empty(broken!.Items);
        Assert.False(healthy.Degraded);
        Assert.True(broken.Degraded);
    }

    [Fact]
    public async Task CommentsAreServedFromTheUpstream()
    {
        _factory.Comments.Page = new([Comment(1)], 1);

        var page = await _factory.CreateClient()
            .GetFromJsonAsync<Page<CommentSummary>>($"/profiles/{UserId}/comments", Json);

        Assert.False(page!.Degraded);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task CommentsDegradeWithoutAffectingArticles()
    {
        _factory.Articles.Page = new([Article("a1")], 1);
        _factory.Comments.Fault = new HttpRequestException("comment service is down");

        var articles = await _factory.CreateClient()
            .GetFromJsonAsync<Page<ArticleSummary>>($"/profiles/{UserId}/articles", Json);
        var comments = await _factory.CreateClient()
            .GetFromJsonAsync<Page<CommentSummary>>($"/profiles/{UserId}/comments", Json);

        Assert.False(articles!.Degraded);
        Assert.True(comments!.Degraded);
    }

    [Fact]
    public async Task StatsReportsUpstreamTotals()
    {
        _factory.Articles.Page = new([Article("a1")], 42);
        _factory.Comments.Page = new([Comment(1)], 7);

        var stats = await _factory.CreateClient()
            .GetFromJsonAsync<ProfileStatsDto>($"/profiles/{UserId}/stats", Json);

        Assert.Equal(42, stats!.Articles);
        Assert.Equal(7, stats.Comments);
    }

    [Fact]
    public async Task StatsCountsSubscriptionsLocally()
    {
        await _factory.CreateClient().Authenticated(UserId).GetAsync("/profiles/me");
        await _factory.CreateClient().Authenticated("fan-1").PostAsync($"/profiles/{UserId}/follow", null);
        await _factory.CreateClient().Authenticated("fan-2").PostAsync($"/profiles/{UserId}/follow", null);

        var stats = await _factory.CreateClient()
            .GetFromJsonAsync<ProfileStatsDto>($"/profiles/{UserId}/stats", Json);

        Assert.Equal(2, stats!.Followers);
        Assert.Equal(0, stats.Following);
    }

    [Fact]
    public async Task StatsReportsNullNotZeroForAnUnreachableUpstream()
    {
        _factory.Articles.Fault = new HttpRequestException("down");
        _factory.Comments.Page = new([], 3);

        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}/stats");
        var raw = await response.Content.ReadAsStringAsync();

        var stats = JsonSerializer.Deserialize<ProfileStatsDto>(raw, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(stats!.Articles);
        Assert.Equal(3, stats.Comments);
        Assert.Contains("\"articles\":null", raw);
    }

    [Fact]
    public async Task StatsNeverReturns5xxEvenWithEverythingDown()
    {
        _factory.Articles.Fault = new HttpRequestException("down");
        _factory.Comments.Fault = new TaskCanceledException("timed out");

        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await response.Content.ReadFromJsonAsync<ProfileStatsDto>(Json);
        Assert.Null(stats!.Articles);
        Assert.Null(stats.Comments);
        Assert.Equal(0, stats.Followers);
    }

    [Fact]
    public async Task StatsAsksTheUpstreamsForOneItemOnly()
    {
        await _factory.CreateClient().GetAsync($"/profiles/{UserId}/stats");

        Assert.Equal(1, _factory.Articles.LastLimit);
        Assert.Equal(1, _factory.Articles.Calls);
        Assert.Equal(1, _factory.Comments.Calls);
    }

    [Fact]
    public async Task ActivityWorksForAUserWithoutAProfileRow()
    {
        _factory.Articles.Page = new([Article("a1")], 1);

        var page = await _factory.CreateClient()
            .GetFromJsonAsync<Page<ArticleSummary>>("/profiles/never-here/articles", Json);

        Assert.False(page!.Degraded);
        Assert.Single(page.Items);
    }
}
