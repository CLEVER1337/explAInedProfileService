using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class ProfileEndpointsTests : IClassFixture<ProfileWebApplicationFactory>
{
    private const string UserId = "user-1";
    private const string OtherId = "user-2";

    private readonly ProfileWebApplicationFactory _factory;

    public ProfileEndpointsTests(ProfileWebApplicationFactory factory)
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

    private Profile? Reload(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.Profiles.AsNoTracking().FirstOrDefault(p => p.UserId == userId);
    }

    [Fact]
    public async Task GetMeRequiresAToken()
    {
        var response = await _factory.CreateClient().GetAsync("/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMeCreatesTheProfileOnFirstCall()
    {
        Assert.Null(Reload(UserId));

        var response = await _factory.CreateClient().Authenticated(UserId).GetAsync("/profiles/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Profile>();
        Assert.Equal(UserId, body!.UserId);
        Assert.Null(body.DisplayName);
        Assert.NotNull(Reload(UserId));
    }

    [Fact]
    public async Task GetMeTwiceLeavesOneRow()
    {
        var client = _factory.CreateClient().Authenticated(UserId);

        await client.GetAsync("/profiles/me");
        await client.GetAsync("/profiles/me");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Profiles.CountAsync(p => p.UserId == UserId));
    }

    [Fact]
    public async Task GetByIdReturns404ForSomeoneWhoHasNeverSignedIn()
    {
        var response = await _factory.CreateClient().GetAsync($"/profiles/{OtherId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByIdIsPublicOnceTheProfileExists()
    {
        await _factory.CreateClient().Authenticated(UserId).GetAsync("/profiles/me");

        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserId, (await response.Content.ReadFromJsonAsync<Profile>())!.UserId);
    }

    [Fact]
    public async Task PutMeWritesTheProfile()
    {
        var client = _factory.CreateClient().Authenticated(UserId);

        var response = await client.PutAsJsonAsync("/profiles/me", new UpdateProfileDto(
            "Ада", "про распределённые системы", "Тбилиси", "https://example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var stored = Reload(UserId)!;
        Assert.Equal("Ада", stored.DisplayName);
        Assert.Equal("https://example.com", stored.WebsiteUrl);
    }

    [Fact]
    public async Task PutMeCreatesTheProfileWhenItDoesNotExistYet()
    {
        Assert.Null(Reload(UserId));

        await _factory.CreateClient().Authenticated(UserId)
            .PutAsJsonAsync("/profiles/me", new UpdateProfileDto("Ада", null, null, null));

        Assert.Equal("Ада", Reload(UserId)!.DisplayName);
    }

    [Fact]
    public async Task PutMeOnlyEverTouchesTheCallersOwnRow()
    {
        await _factory.CreateClient().Authenticated(OtherId).GetAsync("/profiles/me");

        await _factory.CreateClient().Authenticated(UserId)
            .PutAsJsonAsync("/profiles/me", new UpdateProfileDto("Ада", null, null, null));

        Assert.Equal("Ада", Reload(UserId)!.DisplayName);
        Assert.Null(Reload(OtherId)!.DisplayName);
    }

    [Fact]
    public async Task PutMeRequiresAToken()
    {
        var response = await _factory.CreateClient()
            .PutAsJsonAsync("/profiles/me", new UpdateProfileDto("Ада", null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutMeRejectsAnOverlongDisplayName()
    {
        var response = await _factory.CreateClient().Authenticated(UserId)
            .PutAsJsonAsync("/profiles/me", new UpdateProfileDto(new string('x', 101), null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMeRejectsAnOverlongBio()
    {
        var response = await _factory.CreateClient().Authenticated(UserId)
            .PutAsJsonAsync("/profiles/me", new UpdateProfileDto(null, new string('x', 2001), null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMeRejectsANonHttpWebsite()
    {
        var response = await _factory.CreateClient().Authenticated(UserId)
            .PutAsJsonAsync("/profiles/me", new UpdateProfileDto(null, null, null, "javascript:alert(1)"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMeRejectsARelativeWebsite()
    {
        var response = await _factory.CreateClient().Authenticated(UserId)
            .PutAsJsonAsync("/profiles/me", new UpdateProfileDto(null, null, null, "example.com"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BatchPreservesRequestOrder()
    {
        foreach (var id in new[] { "a", "b", "c" })
        {
            await _factory.CreateClient().Authenticated(id).GetAsync("/profiles/me");
        }

        var response = await _factory.CreateClient().GetAsync("/profiles/batch?ids=c,a,b");
        var body = await response.Content.ReadFromJsonAsync<List<Profile>>();

        Assert.Equal(["c", "a", "b"], body!.Select(p => p.UserId));
    }

    [Fact]
    public async Task BatchDropsIdsWithoutAProfile()
    {
        await _factory.CreateClient().Authenticated("a").GetAsync("/profiles/me");

        var response = await _factory.CreateClient().GetAsync("/profiles/batch?ids=a,ghost");
        var body = await response.Content.ReadFromJsonAsync<List<Profile>>();

        Assert.Equal(["a"], body!.Select(p => p.UserId));
    }

    [Fact]
    public async Task BatchRequiresIds()
    {
        var response = await _factory.CreateClient().GetAsync("/profiles/batch");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BatchRejectsMoreThanOneHundredIds()
    {
        var ids = string.Join(',', Enumerable.Range(0, 101).Select(i => $"u{i}"));

        var response = await _factory.CreateClient().GetAsync($"/profiles/batch?ids={ids}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BatchAcceptsExactlyOneHundredIds()
    {
        var ids = string.Join(',', Enumerable.Range(0, 100).Select(i => $"u{i}"));

        var response = await _factory.CreateClient().GetAsync($"/profiles/batch?ids={ids}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BatchIsPublic()
    {
        var response = await _factory.CreateClient().GetAsync("/profiles/batch?ids=anything");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MeAndBatchWinOverTheUserIdRoute()
    {
        var response = await _factory.CreateClient().Authenticated(UserId).GetAsync("/profiles/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserId, (await response.Content.ReadFromJsonAsync<Profile>())!.UserId);
        Assert.Null(Reload("me"));
    }
}
