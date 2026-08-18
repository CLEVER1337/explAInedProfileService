using Microsoft.EntityFrameworkCore;

public class ProfileServiceTests
{
    [Fact]
    public async Task GetReturnsNullForAnUnknownUser()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        Assert.Null(await service.GetAsync("nobody"));
    }

    [Fact]
    public async Task GetOrCreateMaterialisesTheRowWithNoDisplayName()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        var created = await service.GetOrCreateAsync("user-1");

        Assert.Equal("user-1", created.UserId);
        Assert.Null(created.DisplayName);
        Assert.False(created.HasAvatar);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Single(await db.Profiles.ToListAsync());
    }

    [Fact]
    public async Task GetOrCreateIsIdempotent()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        await service.GetOrCreateAsync("user-1");
        await service.GetOrCreateAsync("user-1");

        Assert.Single(await db.Profiles.ToListAsync());
    }

    [Fact]
    public async Task UpdateWritesTheFieldsAndMovesUpdatedAt()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        var profile = await service.GetOrCreateAsync("user-1");
        var before = profile.UpdatedAt;

        await service.UpdateAsync(profile, new UpdateProfileDto(
            "Ада", "пишет про распределённые системы", "Тбилиси", "https://example.com"));

        var stored = await db.Profiles.AsNoTracking().FirstAsync(p => p.UserId == "user-1");
        Assert.Equal("Ада", stored.DisplayName);
        Assert.Equal("пишет про распределённые системы", stored.Bio);
        Assert.Equal("Тбилиси", stored.Location);
        Assert.Equal("https://example.com", stored.WebsiteUrl);
        Assert.True(stored.UpdatedAt >= before);
    }

    [Fact]
    public async Task UpdateTrimsAndCollapsesBlankValuesToNull()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        var profile = await service.GetOrCreateAsync("user-1");
        await service.UpdateAsync(profile, new UpdateProfileDto("  Ада  ", "   ", "", null));

        var stored = await db.Profiles.AsNoTracking().FirstAsync(p => p.UserId == "user-1");
        Assert.Equal("Ада", stored.DisplayName);
        Assert.Null(stored.Bio);
        Assert.Null(stored.Location);
        Assert.Null(stored.WebsiteUrl);
    }

    [Fact]
    public async Task UpdateInvalidatesTheCachedRow()
    {
        using var db = TestHelpers.NewDb();
        var cache = TestHelpers.NewCache();
        var service = TestHelpers.NewProfileService(db, cache);

        var profile = await service.GetOrCreateAsync("user-1");
        await service.GetAsync("user-1");                       // populates the cache
        await service.UpdateAsync(profile, new UpdateProfileDto("Ада", null, null, null));

        Assert.Equal("Ада", (await service.GetAsync("user-1"))!.DisplayName);
    }

    [Fact]
    public async Task GetManyPreservesInputOrder()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        foreach (var id in new[] { "a", "b", "c" }) await service.GetOrCreateAsync(id);

        var result = await service.GetManyAsync(["c", "a", "b"]);

        Assert.Equal(["c", "a", "b"], result.Select(p => p.UserId));
    }

    [Fact]
    public async Task GetManyDropsUnknownIdsRatherThanStubbingThem()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        await service.GetOrCreateAsync("a");

        var result = await service.GetManyAsync(["a", "missing"]);

        Assert.Equal(["a"], result.Select(p => p.UserId));
    }

    [Fact]
    public async Task GetManyOnAnEmptyListTouchesNothing()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        Assert.Empty(await service.GetManyAsync([]));
    }

    [Fact]
    public async Task ExistsDistinguishesKnownFromUnknown()
    {
        using var db = TestHelpers.NewDb();
        var service = TestHelpers.NewProfileService(db);

        await service.GetOrCreateAsync("a");

        Assert.True(await service.ExistsAsync("a"));
        Assert.False(await service.ExistsAsync("b"));
    }
}
