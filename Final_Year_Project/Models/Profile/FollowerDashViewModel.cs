namespace Final_Year_Project.Models.Profile
{
    public class FollowerDashViewModel
    {
        public List<FollowerViewModel>? Followers { get; set; }
        public List<SubscriberViewModel>? Subscribers { get; set; }
        public Dictionary<string, object>? ChartData { get; set; }
    }
}
