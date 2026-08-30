using LFMS.Models;

namespace LFMS.ViewModels;

public class HomeViewModel
{
    public List<Post> Posts { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? Search { get; set; }
    public string? Type { get; set; }
    public int? CategoryId { get; set; }
}
