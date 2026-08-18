public class Subscription
{
    public int Id { get; set; }
    public string FollowerId { get; set; } = default!;
    public string FolloweeId { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
