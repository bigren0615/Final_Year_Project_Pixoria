using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Enums
{
    public enum paymentStatus
    {
        [Display(Name = "Paid")]
        paid,

        [Display(Name = "Unpaid")]

        unpaid,

        [Display(Name = "Refunded")]
        refunded
    }
}
