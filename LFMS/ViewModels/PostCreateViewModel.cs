using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace LFMS.ViewModels;

public class PostCreateViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Title { get; set; } = "";
    [Required, MaxLength(2000)] public string Description { get; set; } = "";
    [MaxLength(2000)] public string PrivateVerificationDetails { get; set; } = "";
    [Required] public string PostType { get; set; } = "Lost";
    [Required, MaxLength(150)] public string Location { get; set; } = "";
    [Required] public DateTime LostFoundDate { get; set; } = DateTime.Today;
    [Required] public int CategoryId { get; set; }
    public List<IFormFile> Images { get; set; } = new();
    public List<string> ExistingImages { get; set; } = new();
}
