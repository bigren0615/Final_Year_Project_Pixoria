using System.ComponentModel.DataAnnotations;
using Final_Year_Project.Services;

namespace Final_Year_Project.Models.SubscriptionPlans
{
    public class PlansAddEditVM
    {
        public int? SPId { get; set; }

        [Required(ErrorMessage = "Plan name is required")]
        [MaxLength(40)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Range(0, 10000)]
        public decimal Price { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public IFormFile? CoverImage { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool RemoveCoverImage { get; set; } = false;

    }
}
