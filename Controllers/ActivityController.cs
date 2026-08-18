using Microsoft.AspNetCore.Mvc;

[Route("profiles")]
public class ActivityController : Controller
{
    private readonly IActivityService _activityService;

    public ActivityController(IActivityService activityService)
    {
        _activityService = activityService;
    }

    [HttpGet]
    [Route("{userId}/articles")]
    public async Task<IResult> GetArticles(
        [FromRoute] string userId, [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
    {
        var page = await _activityService.GetArticlesAsync(
            userId, Paging.Limit(limit), Paging.Offset(offset), ct);

        return Results.Ok(page);
    }

    [HttpGet]
    [Route("{userId}/comments")]
    public async Task<IResult> GetComments(
        [FromRoute] string userId, [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
    {
        var page = await _activityService.GetCommentsAsync(
            userId, Paging.Limit(limit), Paging.Offset(offset), ct);

        return Results.Ok(page);
    }

    [HttpGet]
    [Route("{userId}/stats")]
    public async Task<IResult> GetStats([FromRoute] string userId, CancellationToken ct)
    {
        var stats = await _activityService.GetStatsAsync(userId, ct);

        return Results.Ok(stats);
    }
}
