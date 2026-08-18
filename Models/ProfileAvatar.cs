public class ProfileAvatar
{
    public string UserId { get; set; } = default!;
    public byte[] Content { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string ETag { get; set; } = default!;
    public DateTime UpdatedAt { get; set; }
}
