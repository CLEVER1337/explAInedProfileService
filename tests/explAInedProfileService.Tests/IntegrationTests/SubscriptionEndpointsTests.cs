using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class SubscriptionEndpointsTests : IClassFixture<ProfileWebApplicationFactory>
{
    private const string Follower = "follower-1";
    private const string Followee = "followee-1";

    private readonly ProfileWebApplicationFactory _factory;

    public SubscriptionEndpointsTests(ProfileWebApplicationFactory factory)
    {
        _factory = factory;
        ResetState();
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

    private async Task SeedProfileAsync(string userId) =>
        await _factory.CreateClient().Authenticated(userId).GetAsync("/profiles/me");

    private int CountSubscriptions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.Subscriptions.AsNoTracking().Count();
    }

    [Fact]
    public async Task FollowRequiresAToken()
    {
        var response = await _factory.CreateClient().PostAsync($"/profiles/{Followee}/follow", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FollowingAnUnknownProfileIs404()
    {
        var response = await _factory.CreateClient().Authenticated(Follower)
            .PostAsync("/profiles/ghost/follow", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, CountSubscriptions());
    }

    [Fact]
    public async Task FollowSucceeds()
    {
        await SeedProfileAsync(Followee);

        var response = await _factory.CreateClient().Authenticated(Follower)
            .PostAsync($"/profiles/{Followee}/follow", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, CountSubscriptions());
    }

    [Fact]
    public async Task FollowIsIdempotent()
    {
        await SeedProfileAsync(Followee);
        var client = _factory.CreateClient().Authenticated(Follower);

        var first = await client.PostAsync($"/profiles/{Followee}/follow", null);
        var second = await client.PostAsync($"/profiles/{Followee}/follow", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(1, CountSubscriptions());
    }

    [Fact]
    public async Task FollowingYourselfIsRejected()
    {
        await SeedProfileAsync(Follower);

        var response = await _factory.CreateClient().Authenticated(Follower)
            .PostAsync($"/profiles/{Follower}/follow", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, CountSubscriptions());
    }

    [Fact]
    public async Task FollowMaterialisesTheFollowersOwnProfile()
    {
        await SeedProfileAsync(Followee);

        await _factory.CreateClient().Authenticated(Follower)
            .PostAsync($"/profiles/{Followee}/follow", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Profiles.AnyAsync(p => p.UserId == Follower));
    }

    [Fact]
    public async Task UnfollowRemovesTheEdge()
    {
        await SeedProfileAsync(Followee);
        var client = _factory.CreateClient().Authenticated(Follower);
        await client.PostAsync($"/profiles/{Followee}/follow", null);

        var response = await client.DeleteAsync($"/profiles/{Followee}/follow");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, CountSubscriptions());
    }

    [Fact]
    public async Task UnfollowingSomethingUnfollowedIsStill204()
    {
        var response = await _factory.CreateClient().Authenticated(Follower)
            .DeleteAsync($"/profiles/{Followee}/follow");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task FollowingListsWhoTheCallerFollows()
    {
        await SeedProfileAsync(Followee);
        var client = _factory.CreateClient().Authenticated(Follower);
        await client.PostAsync($"/profiles/{Followee}/follow", null);

        var body = await client.GetFromJsonAsync<List<Profile>>("/profiles/me/following");

        Assert.Equal([Followee], body!.Select(p => p.UserId));
    }

    [Fact]
    public async Task FollowingRequiresAToken()
    {
        var response = await _factory.CreateClient().GetAsync("/profiles/me/following");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FollowersIsPublic()
    {
        await SeedProfileAsync(Followee);
        await _factory.CreateClient().Authenticated(Follower)
            .PostAsync($"/profiles/{Followee}/follow", null);

        var body = await _factory.CreateClient()
            .GetFromJsonAsync<List<Profile>>($"/profiles/{Followee}/followers");

        Assert.Equal([Follower], body!.Select(p => p.UserId));
    }

    [Fact]
    public async Task FollowersOfAnUnknownProfileIsAnEmptyList()
    {
        var response = await _factory.CreateClient().GetAsync("/profiles/ghost/followers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<Profile>>())!);
    }

    [Fact]
    public async Task FollowingClampsAnAbsurdLimit()
    {
        await SeedProfileAsync(Followee);
        var client = _factory.CreateClient().Authenticated(Follower);
        await client.PostAsync($"/profiles/{Followee}/follow", null);

        var response = await client.GetAsync("/profiles/me/following?limit=100000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Content.ReadFromJsonAsync<List<Profile>>())!);
    }
}
