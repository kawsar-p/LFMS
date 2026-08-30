using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LFMS.ViewModels;

public class AdminUserViewModel
{
    public string Id { get; set; } = "";

    [Required, StringLength(100)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Phone, StringLength(20)]
    public string PhoneNumber { get; set; } = "";

    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    public string? ConfirmPassword { get; set; }

    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public string Role { get; set; } = "User";

    public string? ProfileImagePath { get; set; }
    public IFormFile? ProfileImage { get; set; }
}
