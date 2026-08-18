using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfileAvatar> ProfileAvatars => Set<ProfileAvatar>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Profile>(entity =>
        {
            entity.HasKey(p => p.UserId);
            entity.Property(p => p.UserId).HasMaxLength(64);
            entity.Property(p => p.DisplayName).HasMaxLength(100);
            entity.Property(p => p.Bio).HasMaxLength(2000);
            entity.Property(p => p.Location).HasMaxLength(100);
            entity.Property(p => p.WebsiteUrl).HasMaxLength(512);
        });

        builder.Entity<ProfileAvatar>(entity =>
        {
            entity.HasKey(a => a.UserId);
            entity.Property(a => a.UserId).HasMaxLength(64);
            entity.Property(a => a.Content).IsRequired();
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(64);
            entity.Property(a => a.ETag).IsRequired().HasMaxLength(64);
            entity.HasOne<Profile>()
                .WithOne()
                .HasForeignKey<ProfileAvatar>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Subscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.FollowerId).IsRequired().HasMaxLength(64);
            entity.Property(s => s.FolloweeId).IsRequired().HasMaxLength(64);
            entity.HasIndex(s => new { s.FollowerId, s.FolloweeId }).IsUnique();
            entity.HasIndex(s => s.FolloweeId);
        });
    }
}
