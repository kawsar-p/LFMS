using System.ComponentModel.DataAnnotations;

namespace LFMS.ViewModels;

public class AdminSettingsViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Admin email")]
    public string Email { get; set; } = "";

    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    [StringLength(100, MinimumLength = 6)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string? ConfirmNewPassword { get; set; }
}
