namespace LFMS.ViewModels;

public class AdminDashboardViewModel
{
    public int Users { get; set; }
    public int Posts { get; set; }
    public int Comments { get; set; }
    public int Lost { get; set; }
    public int Found { get; set; }
    public int Collected { get; set; }
    public int Available { get; set; }
    public int UnreadNotifications { get; set; }
    public int CollectionRequests { get; set; }

    public List<string> ChartLabels { get; set; } = new();
    public List<int> ChartLost { get; set; } = new();
    public List<int> ChartFound { get; set; } = new();
}
