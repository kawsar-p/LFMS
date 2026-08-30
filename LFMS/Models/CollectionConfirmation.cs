using System.ComponentModel.DataAnnotations;

namespace LFMS.Models;

/// <summary>
/// Permanent history record proving that a post owner completed the collection/recovery confirmation form.
/// </summary>
public class CollectionConfirmation
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post? Post { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = "";

    [MaxLength(20)]
    public string PhoneNumber { get; set; } = "";

    /// <summary>LostItemReceived or FoundItemReturned</summary>
    [Required, MaxLength(40)]
    public string ConfirmationType { get; set; } = "";

    [Required, MaxLength(2000)]
    public string IdentificationDetails { get; set; } = "";

    [Required, MaxLength(2000)]
    public string HandoverDetails { get; set; } = "";

    public DateTime HandoverDate { get; set; } = DateTime.Today;

    [Required, MaxLength(100)]
    public string Status { get; set; } = "Submitted";

    public DateTime ConfirmedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? ClaimantVerificationAnswer { get; set; }

    [MaxLength(2000)]
    public string? VerificationReferenceAtSubmission { get; set; }

    [MaxLength(450)]
    public string? OwnerApprovalUserId { get; set; }
    public DateTime? OwnerApprovedAt { get; set; }

    [MaxLength(450)]
    public string? AdminApprovalUserId { get; set; }
    public DateTime? AdminApprovedAt { get; set; }

    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }
}
