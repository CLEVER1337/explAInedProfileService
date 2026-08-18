using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class FakeArticleClient : IArticleClient
{
    public UpstreamPage<ArticleSummary> Page { get; set; } = new([], 0);
    public Exception? Fault { get; set; }
    public string? LastAuthorId { get; private set; }
    public int LastLimit { get; private set; }
    public int LastOffset { get; private set; }
    public int Calls { get; private set; }

    public Task<UpstreamPage<ArticleSummary>> GetByAuthorAsync(
        string authorId, int limit, int offset, CancellationToken ct)
    {
        Calls++;
        LastAuthorId = authorId;
        LastLimit = limit;
        LastOffset = offset;

        if (Fault is not null) return Task.FromException<UpstreamPage<ArticleSummary>>(Fault);

        return Task.FromResult(Page);
    }

    public void Reset()
    {
        Page = new([], 0);
        Fault = null;
        LastAuthorId = null;
        LastLimit = 0;
        LastOffset = 0;
        Calls = 0;
    }
}

public sealed class FakeCommentClient : ICommentClient
{
    public UpstreamPage<CommentSummary> Page { get; set; } = new([], 0);
    public Exception? Fault { get; set; }
    public string? LastAuthorId { get; private set; }
    public int Calls { get; private set; }

    public Task<UpstreamPage<CommentSummary>> GetByAuthorAsync(
        string authorId, int limit, int offset, CancellationToken ct)
    {
        Calls++;
        LastAuthorId = authorId;

        if (Fault is not null) return Task.FromException<UpstreamPage<CommentSummary>>(Fault);

        return Task.FromResult(Page);
    }

    public void Reset()
    {
        Page = new([], 0);
        Fault = null;
        LastAuthorId = null;
        Calls = 0;
    }
}

public static class TestHelpers
{
    public static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"profile-tests-{Guid.NewGuid()}")
            .Options);

    public static CacheService NewCache() =>
        new(NewDistributedCache(), NullLogger<CacheService>.Instance);

    public static IDistributedCache NewDistributedCache() =>
        new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new()));

    public static ProfileService NewProfileService(ApplicationDbContext db, CacheService? cache = null) =>
        new(db, cache ?? NewCache(), NullLogger<ProfileService>.Instance);

    public static ActivityService NewActivityService(
        IArticleClient articles, ICommentClient comments, ISubscriptionService subscriptions) =>
        new(articles, comments, subscriptions, NullLogger<ActivityService>.Instance);
}
