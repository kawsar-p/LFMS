using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

public class Comment
{
    public int Id { get; set; }
    [Required, MaxLength(500)] public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
}
