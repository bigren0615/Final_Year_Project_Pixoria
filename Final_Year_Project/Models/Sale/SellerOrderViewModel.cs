using System;

namespace Final_Year_Project.Models.Sales
{
    public class SellerOrderViewModel
    {
        public string? OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public float TotalAmount { get; set; } 
        public string? PaymentStatus { get; set; }
        public string? DeliveryStatus { get; set; }
        public string? OrderStatus { get; set; }
        public string? BuyerName { get; set; }
        public string? ShippingAddress { get; set; }
        public List<OrderItemDetail> Items { get; set; } = new List<OrderItemDetail>();
        public List<string> AllowedPayment { get; set; } = new List<string>();
        public List<string> AllowedOrder { get; set; } = new List<string>();
        public List<string> AllowedDelivery { get; set; } = new List<string>();
        public List<RefundDetail> RefundRequests { get; set; } = new List<RefundDetail>();
    }

    public class OrderItemDetail
    {
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public float Price { get; set; }
        public string? ImagePath { get; set; }
        public List<string> AllImages { get; set; } = new List<string>();
        public long? OrderItemId { get; set; }
    }

    public class RefundDetail
    {
        public long RefundId { get; set; }
        public long OrderItemId { get; set; }
        public string? ProductName { get; set; }
        public string? Reason { get; set; }
        public string? RefundStatus { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime DueDate { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public string? CustomerName { get; set; }
    }
}
