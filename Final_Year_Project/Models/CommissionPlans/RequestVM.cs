using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.CommissionPlans
{
    public class RequestAddEditVM
    {
        public int? RId { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

    }
}
