using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string FullName { get; set; } = "";

    // Admin can temporarily disable a user without deleting the account.
    public bool IsActive { get; set; } = true;

    [MaxLength(300)]
    public string? ProfileImagePath { get; set; }
}
