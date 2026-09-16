using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

public class Category
{
    public int Id { get; set; }
    [Required, MaxLength(60)] public string Name { get; set; } = "";
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
