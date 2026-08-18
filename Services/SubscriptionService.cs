using Microsoft.EntityFrameworkCore;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly IProfileService _profileService;

    public SubscriptionService(ApplicationDbContext db, IProfileService profileService)
    {
        _db = db;
        _profileService = profileService;
    }

    public async Task<bool> FollowAsync(string followerId, string followeeId)
    {
        var exists = await _db.Subscriptions
            .AnyAsync(s => s.FollowerId == followerId && s.FolloweeId == followeeId);

        if (exists) return false;

        _db.Subscriptions.Add(new Subscription
        {
            FollowerId = followerId,
            FolloweeId = followeeId,
            CreatedAt = DateTime.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> UnfollowAsync(string followerId, string followeeId)
    {
        var existing = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.FollowerId == followerId && s.FolloweeId == followeeId);

        if (existing is null) return false;

        _db.Subscriptions.Remove(existing);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<IReadOnlyList<Profile>> GetFollowingAsync(string followerId, int limit, int offset)
    {
        var ids = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.FollowerId == followerId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip(offset)
            .Take(limit)
            .Select(s => s.FolloweeId)
            .ToListAsync();

        return await _profileService.GetManyAsync(ids);
    }

    public async Task<IReadOnlyList<Profile>> GetFollowersAsync(string followeeId, int limit, int offset)
    {
        var ids = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.FolloweeId == followeeId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip(offset)
            .Take(limit)
            .Select(s => s.FollowerId)
            .ToListAsync();

        return await _profileService.GetManyAsync(ids);
    }

    public Task<int> CountFollowingAsync(string followerId) =>
        _db.Subscriptions.AsNoTracking().CountAsync(s => s.FollowerId == followerId);

    public Task<int> CountFollowersAsync(string followeeId) =>
        _db.Subscriptions.AsNoTracking().CountAsync(s => s.FolloweeId == followeeId);
}
