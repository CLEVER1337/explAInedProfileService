public interface ISubscriptionService
{
    Task<bool> FollowAsync(string followerId, string followeeId);

    Task<bool> UnfollowAsync(string followerId, string followeeId);

    Task<IReadOnlyList<Profile>> GetFollowingAsync(string followerId, int limit, int offset);

    Task<IReadOnlyList<Profile>> GetFollowersAsync(string followeeId, int limit, int offset);

    Task<int> CountFollowingAsync(string followerId);

    Task<int> CountFollowersAsync(string followeeId);
}
