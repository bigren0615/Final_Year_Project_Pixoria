using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Enums
{
    public enum refundStatus
    {
        [Display(Name = "PENDING")]
        pending,

        [Display(Name = "APPROVED")]
        approved,

        [Display(Name = "REJECTED")]
        rejected,

        [Display(Name = "COMPLETED")]
        completed
    }
}
