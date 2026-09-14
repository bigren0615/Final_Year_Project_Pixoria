namespace Final_Year_Project.Models.CommissionPlans
{
    public class ManageRequestViewModel
    {
        public List<ManageRequestItemVM> SentRequests { get; set; } = new();
        public List<ManageRequestItemVM> ReceivedRequests { get; set; } = new();
        public bool IsArtist { get; set; }
    }
}
