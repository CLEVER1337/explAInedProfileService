public interface IAvatarService
{
    Task<ProfileAvatar?> GetAsync(string userId);

    Task StoreAsync(Profile profile, byte[] content, string contentType);

    Task<bool> DeleteAsync(string userId);
}
