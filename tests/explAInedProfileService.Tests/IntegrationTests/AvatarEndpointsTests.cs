using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class AvatarEndpointsTests : IClassFixture<ProfileWebApplicationFactory>
{
    private const string UserId = "avatar-user";

    private const int MaxBytes = 1024;

    private readonly ProfileWebApplicationFactory _factory;

    public AvatarEndpointsTests(ProfileWebApplicationFactory factory)
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

    private static byte[] Png(int size = 64)
    {
        var bytes = new byte[Math.Max(size, 8)];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] Jpeg(int size = 64)
    {
        var bytes = new byte[Math.Max(size, 3)];
        byte[] signature = [0xFF, 0xD8, 0xFF];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    private static MultipartFormDataContent Upload(byte[] content, string contentType)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { file, "file", "avatar.bin" } };
    }

    private async Task<HttpResponseMessage> PutAsync(byte[] content, string contentType, string? userId = null) =>
        await _factory.CreateClient().Authenticated(userId ?? UserId)
            .PutAsync("/profiles/me/avatar", Upload(content, contentType));

    private ProfileAvatar? Reload(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.ProfileAvatars.AsNoTracking().FirstOrDefault(a => a.UserId == userId);
    }

    private bool HasAvatarFlag(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.Profiles.AsNoTracking().First(p => p.UserId == userId).HasAvatar;
    }

    [Fact]
    public async Task PutRequiresAToken()
    {
        var response = await _factory.CreateClient()
            .PutAsync("/profiles/me/avatar", Upload(Png(), "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutStoresThePngAndSetsTheFlag()
    {
        var response = await PutAsync(Png(), "image/png");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var stored = Reload(UserId);
        Assert.NotNull(stored);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(64, stored.Content.Length);
        Assert.Equal(64, stored.ETag.Length);
        Assert.True(HasAvatarFlag(UserId));
    }

    [Fact]
    public async Task PutWithoutAFileIsRejected()
    {
        var response = await _factory.CreateClient().Authenticated(UserId)
            .PutAsync("/profiles/me/avatar", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutRejectsAPayloadOverTheConfiguredLimit()
    {
        var response = await PutAsync(Png(MaxBytes + 1), "image/png");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Null(Reload(UserId));
    }

    [Fact]
    public async Task PutAcceptsExactlyTheLimit()
    {
        var response = await PutAsync(Png(MaxBytes), "image/png");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PutRejectsANonImageBody()
    {
        var response = await PutAsync("<html>hello</html>"u8.ToArray(), "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(Reload(UserId));
    }

    [Fact]
    public async Task PutRejectsABodyThatContradictsTheDeclaredContentType()
    {
        var response = await PutAsync(Jpeg(), "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(Reload(UserId));
    }

    [Fact]
    public async Task PutRejectsAnExecutableDisguisedAsAnImage()
    {
        var response = await PutAsync([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00], "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutReplacesAnExistingAvatarInPlace()
    {
        await PutAsync(Png(), "image/png");
        var first = Reload(UserId)!.ETag;

        await PutAsync(Jpeg(128), "image/jpeg");
        var second = Reload(UserId)!;

        Assert.NotEqual(first, second.ETag);
        Assert.Equal("image/jpeg", second.ContentType);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.ProfileAvatars.CountAsync(a => a.UserId == UserId));
    }

    [Fact]
    public async Task GetReturns404WhenThereIsNoAvatar()
    {
        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}/avatar");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetServesTheBytesWithAnETag()
    {
        await PutAsync(Png(), "image/png");

        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.NotNull(response.Headers.ETag);
        Assert.Equal(64, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task GetIsPublic()
    {
        await PutAsync(Png(), "image/png");

        var response = await _factory.CreateClient().GetAsync($"/profiles/{UserId}/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReturns304ForAMatchingETag()
    {
        await PutAsync(Png(), "image/png");

        var client = _factory.CreateClient();
        var first = await client.GetAsync($"/profiles/{UserId}/avatar");
        var etag = first.Headers.ETag!.ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/profiles/{UserId}/avatar");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task GetServesTheBytesForAStaleETag()
    {
        await PutAsync(Png(), "image/png");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/profiles/{UserId}/avatar");
        request.Headers.TryAddWithoutValidation("If-None-Match", "\"stale\"");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRemovesTheRowAndClearsTheFlag()
    {
        await PutAsync(Png(), "image/png");

        var response = await _factory.CreateClient().Authenticated(UserId)
            .DeleteAsync("/profiles/me/avatar");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(Reload(UserId));
        Assert.False(HasAvatarFlag(UserId));
    }

    [Fact]
    public async Task DeleteWithoutAnAvatarIsStill204()
    {
        var response = await _factory.CreateClient().Authenticated(UserId)
            .DeleteAsync("/profiles/me/avatar");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRequiresAToken()
    {
        var response = await _factory.CreateClient().DeleteAsync("/profiles/me/avatar");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProfileListingsDoNotCarryAvatarBytes()
    {
        await PutAsync(Png(), "image/png");

        var body = await _factory.CreateClient().GetStringAsync($"/profiles/batch?ids={UserId}");

        Assert.Contains("hasAvatar", body);
        Assert.DoesNotContain("content", body, StringComparison.OrdinalIgnoreCase);
    }
}
