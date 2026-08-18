using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

public class AvatarService : IAvatarService
{
    private readonly ApplicationDbContext _db;
    private readonly IProfileService _profileService;

    public AvatarService(ApplicationDbContext db, IProfileService profileService)
    {
        _db = db;
        _profileService = profileService;
    }

    public Task<ProfileAvatar?> GetAsync(string userId) =>
        _db.ProfileAvatars.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId);

    public async Task StoreAsync(Profile profile, byte[] content, string contentType)
    {
        var etag = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var now = DateTime.UtcNow;

        var existing = await _db.ProfileAvatars.FirstOrDefaultAsync(a => a.UserId == profile.UserId);

        if (existing is null)
        {
            _db.ProfileAvatars.Add(new ProfileAvatar
            {
                UserId = profile.UserId,
                Content = content,
                ContentType = contentType,
                ETag = etag,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.Content = content;
            existing.ContentType = contentType;
            existing.ETag = etag;
            existing.UpdatedAt = now;
        }

        profile.HasAvatar = true;
        profile.UpdatedAt = now;

        await _db.SaveChangesAsync();

        await _profileService.InvalidateAsync(profile.UserId);

        ProfileMetrics.AvatarBytes.Observe(content.Length);
    }

    public async Task<bool> DeleteAsync(string userId)
    {
        var existing = await _db.ProfileAvatars.FirstOrDefaultAsync(a => a.UserId == userId);
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing is null)
        {
            if (profile is { HasAvatar: true })
            {
                profile.HasAvatar = false;
                profile.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await _profileService.InvalidateAsync(userId);
            }

            return false;
        }

        _db.ProfileAvatars.Remove(existing);

        if (profile is not null)
        {
            profile.HasAvatar = false;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await _profileService.InvalidateAsync(userId);

        return true;
    }
}
