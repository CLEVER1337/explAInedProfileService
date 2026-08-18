public interface IActivityService
{
    Task<ActivityPageDto<ArticleSummary>> GetArticlesAsync(
        string userId, int limit, int offset, CancellationToken ct);

    Task<ActivityPageDto<CommentSummary>> GetCommentsAsync(
        string userId, int limit, int offset, CancellationToken ct);

    Task<ProfileStatsDto> GetStatsAsync(string userId, CancellationToken ct);
}
