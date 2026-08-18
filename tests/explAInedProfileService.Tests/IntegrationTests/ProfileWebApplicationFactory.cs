using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class ProfileWebApplicationFactory : WebApplicationFactory<Program>
{
    public string InMemoryDbName { get; } = $"explAIned-profiles-tests-{Guid.NewGuid()}";

    public FakeArticleClient Articles { get; } = new();

    public FakeCommentClient Comments { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "InMemory",
                ["Database:InMemoryName"] = InMemoryDbName,
                ["Cache:Provider"] = "Memory",
                ["Jwt:Key"] = "JwtSecretPlaceHolder_explAIned32",
                ["Jwt:Issuer"] = "http://localhost:5125/",
                ["Jwt:Audience"] = "http://localhost:5125/",
                ["Avatar:MaxBytes"] = "1024",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IArticleClient>(Articles);
            services.AddSingleton<ICommentClient>(Comments);
        });
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
