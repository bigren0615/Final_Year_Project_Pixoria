using System.ComponentModel.DataAnnotations;
using Final_Year_Project.Enums;
using Final_Year_Project.Services;

namespace Final_Year_Project.Models.CommissionPlans
{
    public class CommissionAddEditVM
    {
        public int? CId { get; set; }
        [Required(ErrorMessage = "Plan name is required")]
        [MaxLength(40)]
        public string Title { get; set; } = string.Empty;
        [Required(ErrorMessage = "Description is required")]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        [Required(ErrorMessage = "Category is required")]
        public commissionPlanCategory Category { get; set; }
        [Required(ErrorMessage = "Target Price is required")]
        [Range(0, 10000)]
        public decimal TargetPrice { get; set; }
        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public IFormFile? Image { get; set; }
        public commissionPlanStatus Status { get; set; }
        public string? ImageUrl { get; set; }
        public bool RemoveImage { get; set; } = false;
    }
}
