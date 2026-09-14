using Final_Year_Project.Enums;

namespace Final_Year_Project.Models.CommissionPlans
{
    public class ManageRequestItemVM
    {
        public int RId { get; set; }
        public int CId { get; set; }
        public string PlanTitle { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? UserProfilePic { get; set; }
        public string RequestDescription { get; set; } = string.Empty;
        public requestStatus Status { get; set; }
        public paymentStatus PaymentStatus { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? Deadline { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ServiceTax { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
