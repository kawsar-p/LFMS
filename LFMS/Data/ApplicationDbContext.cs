using LFMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LFMS.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<PostImage> PostImages => Set<PostImage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<CollectionConfirmation> CollectionConfirmations => Set<CollectionConfirmation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Post>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Comment>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Like>().HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
        builder.Entity<Like>().HasOne(x => x.Post).WithMany(x => x.Likes).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Like>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PostImage>().HasOne(x => x.Post).WithMany(x => x.Images).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Notification>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ChatMessage>().HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ChatMessage>().HasOne(x => x.Receiver).WithMany().HasForeignKey(x => x.ReceiverId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ChatMessage>().HasIndex(x => new { x.SenderId, x.ReceiverId, x.CreatedAt });
        builder.Entity<CollectionConfirmation>().HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CollectionConfirmation>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CollectionConfirmation>().HasIndex(x => new { x.PostId, x.ConfirmedAt });
        builder.Entity<CollectionConfirmation>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerApprovalUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CollectionConfirmation>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AdminApprovalUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
