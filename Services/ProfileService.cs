using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class ProfileService : IProfileService
{
    private const int CacheTtlSeconds = 60;

    private readonly ApplicationDbContext _db;
    private readonly CacheService _cache;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(ApplicationDbContext db, CacheService cache, ILogger<ProfileService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Profile?> GetAsync(string userId)
    {
        var key = CacheKey(userId);

        try
        {
            var cached = await _cache.GetValue(key);
            if (cached is not null)
            {
                var deserialized = JsonSerializer.Deserialize<Profile>(cached);
                if (deserialized is not null) return deserialized;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for {Key}", key);
        }

        var profile = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null) return null;

        try
        {
            await _cache.SetValue(key, JsonSerializer.Serialize(profile), TimeSpan.FromSeconds(CacheTtlSeconds));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }

        return profile;
    }

    public async Task<Profile> GetOrCreateAsync(string userId)
    {
        var existing = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (existing is not null) return existing;

        var now = DateTime.UtcNow;
        var created = new Profile
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Profiles.Add(created);

        try
        {
            await _db.SaveChangesAsync();
            ProfileMetrics.LazyCreated.Inc();
        }
        catch (DbUpdateException)
        {
            _db.Entry(created).State = EntityState.Detached;

            var raced = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (raced is null) throw;

            return raced;
        }

        return created;
    }

    public async Task<IReadOnlyList<Profile>> GetManyAsync(IReadOnlyList<string> orderedIds)
    {
        if (orderedIds.Count == 0) return [];

        var profiles = await _db.Profiles.AsNoTracking()
            .Where(p => orderedIds.Contains(p.UserId))
            .ToListAsync();

        var map = profiles.ToDictionary(p => p.UserId);

        return orderedIds.Where(map.ContainsKey).Select(id => map[id]).ToList();
    }

    public async Task UpdateAsync(Profile profile, UpdateProfileDto dto)
    {
        profile.DisplayName = Normalize(dto.DisplayName);
        profile.Bio = Normalize(dto.Bio);
        profile.Location = Normalize(dto.Location);
        profile.WebsiteUrl = Normalize(dto.WebsiteUrl);
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await InvalidateAsync(profile.UserId);
    }

    public Task<bool> ExistsAsync(string userId) =>
        _db.Profiles.AsNoTracking().AnyAsync(p => p.UserId == userId);

    public async Task InvalidateAsync(string userId)
    {
        try
        {
            await _cache.RemoveValue(CacheKey(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed for {UserId}", userId);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CacheKey(string userId) => $"profiles:{userId}";
}
