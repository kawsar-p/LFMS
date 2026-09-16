using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

public class Post
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string ReferenceCode { get; set; } = "";
    [Required, MaxLength(120)] public string Title { get; set; } = "";
    [Required, MaxLength(2000)] public string Description { get; set; } = "";
    [Required] public string PostType { get; set; } = "Lost";
    [MaxLength(150)] public string Location { get; set; } = "";
    public DateTime LostFoundDate { get; set; } = DateTime.Today;
    [MaxLength(300)] public string? ImagePath { get; set; }
    /// <summary>Private facts used only for claim verification; never shown on the public post.</summary>
    [MaxLength(2000)] public string? PrivateVerificationDetails { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    [Required, MaxLength(20)] public string Status { get; set; } = "Available";
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<PostImage> Images { get; set; } = new List<PostImage>();
}
