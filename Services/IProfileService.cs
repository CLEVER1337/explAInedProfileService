public interface IProfileService
{
    Task<Profile?> GetAsync(string userId);

    Task<Profile> GetOrCreateAsync(string userId);

    Task<IReadOnlyList<Profile>> GetManyAsync(IReadOnlyList<string> orderedIds);

    Task UpdateAsync(Profile profile, UpdateProfileDto dto);

    Task<bool> ExistsAsync(string userId);

    Task InvalidateAsync(string userId);
}
