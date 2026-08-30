using System.ComponentModel.DataAnnotations;

namespace LFMS.ViewModels;

public class ProfileViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, Phone, StringLength(20)]
    [Display(Name = "Mobile number")]
    public string PhoneNumber { get; set; } = "";

    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    [Display(Name = "Confirm new password")]
    public string? ConfirmNewPassword { get; set; }
}
