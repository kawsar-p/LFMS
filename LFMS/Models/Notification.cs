using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

public class Notification
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = "";

    public ApplicationUser? User { get; set; }

    [Required, MaxLength(500)]
    public string Message { get; set; } = "";

    [MaxLength(300)]
    public string? Link { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
