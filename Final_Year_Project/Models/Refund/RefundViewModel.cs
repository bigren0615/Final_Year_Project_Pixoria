using Final_Year_Project.Models.DB;
using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.Refund
{
    public class RefundRequestViewModel
    {
        public string? OrderId { get; set; }
        public int OrderItemId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductImage { get; set; }
        public float Price { get; set; }
        public int Quantity { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public DateTime? RefundDueDate { get; set; }
        public bool IsRefundable { get; set; }

        [Required(ErrorMessage = "Please provide a reason for the refund")]
        [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
        public string? Reason { get; set; }

        public List<IFormFile>? Images { get; set; }
    }

    public class RefundDetailViewModel
    {
        public DB.Refund Refund { get; set; } = new();
        public List<RefundImage> Images { get; set; } = new();
        public OrderItem? OrderItem { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductName { get; set; }
        public string? ProductImage { get; set; }
    }
}
