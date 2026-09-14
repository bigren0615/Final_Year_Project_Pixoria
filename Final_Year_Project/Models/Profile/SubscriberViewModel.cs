using Final_Year_Project.Enums;

namespace Final_Year_Project.Models.Profile
{
    public class SubscriberViewModel
    {
        public int SId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int UId { get; set; }
        public int SPId { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal ServiceTax { get; set; }
        public decimal TotalAmount { get; set; }
        public subscriptionStatus Status { get; set; }
        public bool? IsRenewal { get; set; }
        public paymentStatus PaymentStatus { get; set; }
        public string? DisplayName { get; set; }
        public string? ProfilePicUrl { get; set; }
        // Plan details
        public string? PlanTitle { get; set; }
        public decimal? PlanPrice { get; set; }
    }
}
