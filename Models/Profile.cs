public class Profile
{
    public string UserId { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }
    public string? WebsiteUrl { get; set; }
    public bool HasAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
