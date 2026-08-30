using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

public class ChatMessage
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string SenderId { get; set; } = "";
    public ApplicationUser? Sender { get; set; }

    [Required, MaxLength(450)]
    public string ReceiverId { get; set; } = "";
    public ApplicationUser? Receiver { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
