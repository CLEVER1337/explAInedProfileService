using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

[Route("profiles")]
public class AvatarController : Controller
{
    private const long HardRequestLimitBytes = 4 * 1024 * 1024;

    private const int DefaultMaxBytes = 1024 * 1024;

    private readonly IAvatarService _avatarService;
    private readonly IProfileService _profileService;
    private readonly int _maxBytes;

    public AvatarController(
        IAvatarService avatarService, IProfileService profileService, IConfiguration configuration)
    {
        _avatarService = avatarService;
        _profileService = profileService;
        _maxBytes = int.TryParse(configuration["Avatar:MaxBytes"], out var configured) && configured > 0
            ? configured
            : DefaultMaxBytes;
    }

    [HttpGet]
    [Route("{userId}/avatar")]
    public async Task<IResult> Get([FromRoute] string userId)
    {
        var avatar = await _avatarService.GetAsync(userId);
        if (avatar is null)
        {
            return Results.NotFound();
        }

        var quoted = $"\"{avatar.ETag}\"";

        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var provided)
            && provided.Any(value => value is not null && value.Split(',').Any(tag => tag.Trim() == quoted)))
        {
            Response.Headers.ETag = quoted;
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = quoted;

        return Results.File(avatar.Content, avatar.ContentType, lastModified: avatar.UpdatedAt);
    }

    [HttpPut]
    [Route("me/avatar")]
    [Authorize]
    [RequestSizeLimit(HardRequestLimitBytes)]
    public async Task<IResult> Put(IFormFile? file)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest("file is required");
        }

        if (file.Length > _maxBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var content = buffer.ToArray();

        if (content.Length > _maxBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var validation = AvatarValidator.Validate(content, file.ContentType);
        if (!validation.Ok)
        {
            return Results.BadRequest(validation.Error);
        }

        var profile = await _profileService.GetOrCreateAsync(userId);

        try
        {
            await _avatarService.StoreAsync(profile, content, validation.ContentType!);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    [HttpDelete]
    [Route("me/avatar")]
    [Authorize]
    public async Task<IResult> Delete()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        await _avatarService.DeleteAsync(userId);

        return Results.NoContent();
    }
}
