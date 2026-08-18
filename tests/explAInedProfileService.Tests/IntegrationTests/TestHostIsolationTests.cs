using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

public class TestHostIsolationTests : IClassFixture<ProfileWebApplicationFactory>
{
    private readonly ProfileWebApplicationFactory _factory;

    public TestHostIsolationTests(ProfileWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void TheDatabaseIsInMemory()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(db.Database.IsRelational());
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", db.Database.ProviderName);
    }

    [Fact]
    public void EachFactoryGetsItsOwnDatabase()
    {
        using var other = new ProfileWebApplicationFactory();

        Assert.NotEqual(_factory.InMemoryDbName, other.InMemoryDbName);
    }

    [Fact]
    public void TheCacheIsInProcess()
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        Assert.IsType<MemoryDistributedCache>(cache);
    }

    [Fact]
    public void UpstreamClientsAreTheTestDoubles()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.Same(_factory.Articles, scope.ServiceProvider.GetRequiredService<IArticleClient>());
        Assert.Same(_factory.Comments, scope.ServiceProvider.GetRequiredService<ICommentClient>());
    }
}
