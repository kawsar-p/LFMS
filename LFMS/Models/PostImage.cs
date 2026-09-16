using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

public class PostImage
{
    public int Id { get; set; }
    [Required, MaxLength(300)] public string ImagePath { get; set; } = "";
    public int PostId { get; set; }
    public Post? Post { get; set; }
}
