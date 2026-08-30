namespace LFMS.ViewModels;

public class PublicProfileViewModel
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? ProfileImagePath { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? MemberSince { get; set; }
    public int PostCount { get; set; }
    public int CollectedCount { get; set; }
    public bool CanMessage { get; set; }
}
