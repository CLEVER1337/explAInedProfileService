public interface ICommentClient
{
    Task<UpstreamPage<CommentSummary>> GetByAuthorAsync(
        string authorId, int limit, int offset, CancellationToken ct);
}
