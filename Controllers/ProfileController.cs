using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("profiles")]
public class ProfileController : Controller
{
    private const int BatchMaxIds = 100;
    private const int DisplayNameMaxLength = 100;
    private const int BioMaxLength = 2000;
    private const int LocationMaxLength = 100;
    private const int WebsiteUrlMaxLength = 512;

    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    [Route("batch")]
    public async Task<IResult> GetByIds([FromQuery] string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return Results.BadRequest("ids is required");
        }

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (idList.Length == 0)
        {
            return Results.BadRequest("ids is required");
        }

        if (idList.Length > BatchMaxIds)
        {
            return Results.BadRequest($"at most {BatchMaxIds} ids per request");
        }

        var profiles = await _profileService.GetManyAsync(idList);

        return Results.Ok(profiles);
    }

    [HttpGet]
    [Route("me")]
    [Authorize]
    public async Task<IResult> GetOwn()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var profile = await _profileService.GetOrCreateAsync(userId);

        return Results.Ok(profile);
    }

    [HttpPut]
    [Route("me")]
    [Authorize]
    public async Task<IResult> UpdateOwn([FromBody] UpdateProfileDto dto)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var validationError = Validate(dto);
        if (validationError is not null)
        {
            return Results.BadRequest(validationError);
        }

        var profile = await _profileService.GetOrCreateAsync(userId);

        try
        {
            await _profileService.UpdateAsync(profile, dto);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    [HttpGet]
    [Route("{userId}")]
    public async Task<IResult> GetById([FromRoute] string userId)
    {
        var profile = await _profileService.GetAsync(userId);

        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static string? Validate(UpdateProfileDto dto)
    {
        if (dto.DisplayName is { Length: > DisplayNameMaxLength })
        {
            return $"DisplayName is too long (max {DisplayNameMaxLength} characters)";
        }

        if (dto.Bio is { Length: > BioMaxLength })
        {
            return $"Bio is too long (max {BioMaxLength} characters)";
        }

        if (dto.Location is { Length: > LocationMaxLength })
        {
            return $"Location is too long (max {LocationMaxLength} characters)";
        }

        if (dto.WebsiteUrl is { Length: > WebsiteUrlMaxLength })
        {
            return $"WebsiteUrl is too long (max {WebsiteUrlMaxLength} characters)";
        }

        if (!string.IsNullOrWhiteSpace(dto.WebsiteUrl) && !IsHttpUrl(dto.WebsiteUrl))
        {
            return "WebsiteUrl must be an absolute http or https URL";
        }

        return null;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
