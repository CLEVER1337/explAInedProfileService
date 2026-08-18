using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("profiles")]
public class SubscriptionController : Controller
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly IProfileService _profileService;

    public SubscriptionController(ISubscriptionService subscriptionService, IProfileService profileService)
    {
        _subscriptionService = subscriptionService;
        _profileService = profileService;
    }

    [HttpPost]
    [Route("{userId}/follow")]
    [Authorize]
    public async Task<IResult> Follow([FromRoute] string userId)
    {
        var followerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (followerId is null)
        {
            return Results.Unauthorized();
        }

        if (string.Equals(followerId, userId, StringComparison.Ordinal))
        {
            return Results.BadRequest("Cannot follow yourself");
        }

        if (!await _profileService.ExistsAsync(userId))
        {
            return Results.NotFound();
        }

        await _profileService.GetOrCreateAsync(followerId);

        await _subscriptionService.FollowAsync(followerId, userId);

        return Results.NoContent();
    }

    [HttpDelete]
    [Route("{userId}/follow")]
    [Authorize]
    public async Task<IResult> Unfollow([FromRoute] string userId)
    {
        var followerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (followerId is null)
        {
            return Results.Unauthorized();
        }

        await _subscriptionService.UnfollowAsync(followerId, userId);

        return Results.NoContent();
    }

    [HttpGet]
    [Route("me/following")]
    [Authorize]
    public async Task<IResult> GetOwnFollowing([FromQuery] int? limit, [FromQuery] int? offset)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var following = await _subscriptionService.GetFollowingAsync(
            userId, Paging.Limit(limit), Paging.Offset(offset));

        return Results.Ok(following);
    }

    [HttpGet]
    [Route("{userId}/followers")]
    public async Task<IResult> GetFollowers(
        [FromRoute] string userId, [FromQuery] int? limit, [FromQuery] int? offset)
    {
        var followers = await _subscriptionService.GetFollowersAsync(
            userId, Paging.Limit(limit), Paging.Offset(offset));

        return Results.Ok(followers);
    }
}
