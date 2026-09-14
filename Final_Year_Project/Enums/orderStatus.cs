using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Enums
{
    public enum orderPaymentStatus
    {
        [Display(Name = "PENDING")]
        pending,

        [Display(Name = "PAID")]
        paid,

        [Display(Name = "REFUNDED")]
        refunded,
    }

    public enum deliveryStatus
    {
        [Display(Name = "PENDING")]
        pending,

        [Display(Name = "PACKED")]
        packed,

        [Display(Name = "SHIPPED")]
        shipped,

        [Display(Name = "DELIVERED")]
        delivered,

        [Display(Name = "RETURNED")]
        returned
    }

    public enum orderStatus
    {
        [Display(Name = "PENDING")]
        pending,

        [Display(Name = "CONFIRMED")]
        confirmed,

        [Display(Name = "PROCESSING")]
        processing,

        [Display(Name = "COMPLETED")]
        completed,

        [Display(Name = "CANCELLED")]
        cancelled,

        [Display(Name = "REFUNDED")]
        refunded
    }
}
