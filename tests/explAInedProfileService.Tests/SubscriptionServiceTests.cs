using Microsoft.EntityFrameworkCore;

public class SubscriptionServiceTests
{
    private static async Task<(ApplicationDbContext Db, SubscriptionService Service)> NewAsync(
        params string[] seedProfiles)
    {
        var db = TestHelpers.NewDb();
        var profiles = TestHelpers.NewProfileService(db);

        foreach (var id in seedProfiles) await profiles.GetOrCreateAsync(id);

        return (db, new SubscriptionService(db, profiles));
    }

    [Fact]
    public async Task FollowCreatesTheEdge()
    {
        var (db, service) = await NewAsync("a", "b");
        using (db)
        {
            Assert.True(await service.FollowAsync("a", "b"));
            Assert.Single(await db.Subscriptions.ToListAsync());
        }
    }

    [Fact]
    public async Task FollowingTwiceReportsNoNewEdgeAndStoresOne()
    {
        var (db, service) = await NewAsync("a", "b");
        using (db)
        {
            Assert.True(await service.FollowAsync("a", "b"));
            Assert.False(await service.FollowAsync("a", "b"));
            Assert.Single(await db.Subscriptions.ToListAsync());
        }
    }

    [Fact]
    public async Task UnfollowRemovesTheEdge()
    {
        var (db, service) = await NewAsync("a", "b");
        using (db)
        {
            await service.FollowAsync("a", "b");

            Assert.True(await service.UnfollowAsync("a", "b"));
            Assert.Empty(await db.Subscriptions.ToListAsync());
        }
    }

    [Fact]
    public async Task UnfollowingSomethingThatWasNeverFollowedIsNotAnError()
    {
        var (db, service) = await NewAsync("a", "b");
        using (db)
        {
            Assert.False(await service.UnfollowAsync("a", "b"));
        }
    }

    [Fact]
    public async Task FollowIsDirectional()
    {
        var (db, service) = await NewAsync("a", "b");
        using (db)
        {
            await service.FollowAsync("a", "b");

            Assert.Equal(1, await service.CountFollowingAsync("a"));
            Assert.Equal(0, await service.CountFollowingAsync("b"));
            Assert.Equal(1, await service.CountFollowersAsync("b"));
            Assert.Equal(0, await service.CountFollowersAsync("a"));
        }
    }

    [Fact]
    public async Task FollowingListsProfilesNewestFirst()
    {
        var (db, service) = await NewAsync("a", "b", "c");
        using (db)
        {
            await service.FollowAsync("a", "b");
            await service.FollowAsync("a", "c");

            var following = await service.GetFollowingAsync("a", 20, 0);

            Assert.Equal(["c", "b"], following.Select(p => p.UserId));
        }
    }

    [Fact]
    public async Task FollowersListsProfiles()
    {
        var (db, service) = await NewAsync("a", "b", "c");
        using (db)
        {
            await service.FollowAsync("b", "a");
            await service.FollowAsync("c", "a");

            var followers = await service.GetFollowersAsync("a", 20, 0);

            Assert.Equal(2, followers.Count);
            Assert.Contains("b", followers.Select(p => p.UserId));
            Assert.Contains("c", followers.Select(p => p.UserId));
        }
    }

    [Fact]
    public async Task FollowingRespectsLimitAndOffset()
    {
        var (db, service) = await NewAsync("a", "b", "c", "d");
        using (db)
        {
            await service.FollowAsync("a", "b");
            await service.FollowAsync("a", "c");
            await service.FollowAsync("a", "d");

            var firstPage = await service.GetFollowingAsync("a", 2, 0);
            var secondPage = await service.GetFollowingAsync("a", 2, 2);

            Assert.Equal(2, firstPage.Count);
            Assert.Single(secondPage);
            Assert.Empty(firstPage.Select(p => p.UserId).Intersect(secondPage.Select(p => p.UserId)));
        }
    }

    [Fact]
    public async Task FollowingSkipsFolloweesWithoutAProfileRow()
    {
        var (db, service) = await NewAsync("a");
        using (db)
        {
            db.Subscriptions.Add(new Subscription
            {
                FollowerId = "a", FolloweeId = "ghost", CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            Assert.Empty(await service.GetFollowingAsync("a", 20, 0));
            Assert.Equal(1, await service.CountFollowingAsync("a"));
        }
    }
}
