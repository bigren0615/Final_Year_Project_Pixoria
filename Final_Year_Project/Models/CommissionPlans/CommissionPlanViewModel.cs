using Final_Year_Project.Enums;

namespace Final_Year_Project.Models.CommissionPlans
{
    public class CommissionPlanViewModel
    {
        public int CId { get; set; }
        public int UId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public commissionPlanCategory Category { get; set; }
        public decimal TargetPrice { get; set; }
        public string? Image { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public commissionPlanStatus Status { get; set; }
        public bool IsOwner { get; set; }
        public string? ArtistName { get; set; }
        public string? ArtistProfilePic { get; set; }
    }
}
