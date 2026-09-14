namespace Final_Year_Project.Models.SubscriptionPlans
{
    public class SubscriptionPlanViewModel
    {
        public int SPId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
        public bool Subscribed { get; set; }
        public bool? IsRenewal { get; set; }
        public bool CanSubscribe { get; set; }
        // Upgrade/discount behaviour removed - single subscription per creator enforced
    }
}
