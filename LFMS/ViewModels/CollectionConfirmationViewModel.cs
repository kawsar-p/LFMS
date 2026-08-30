using System.ComponentModel.DataAnnotations;

namespace LFMS.ViewModels;

public class CollectionConfirmationViewModel
{
    public int PostId { get; set; }
    public string PostType { get; set; } = "";
    public string Title { get; set; } = "";

    [Required, StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Phone, StringLength(20)]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = "";

    [Required, StringLength(2000, MinimumLength = 10)]
    [Display(Name = "How did you identify this item?")]
    public string IdentificationDetails { get; set; } = "";

    [Required, StringLength(2000, MinimumLength = 5)]
    [Display(Name = "Private verification answer")]
    public string ClaimantVerificationAnswer { get; set; } = "";

    [Required, StringLength(2000, MinimumLength = 10)]
    [Display(Name = "Collection / handover details")]
    public string HandoverDetails { get; set; } = "";

    [DataType(DataType.Date)]
    [Display(Name = "Confirmation date")]
    public DateTime HandoverDate { get; set; } = DateTime.Today;

    [Display(Name = "Confirmation")]
    public bool Confirmed { get; set; }
}
