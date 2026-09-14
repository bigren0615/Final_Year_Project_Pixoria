namespace Final_Year_Project.Models.OrderHistory
{
    public class OrderHistoryViewModel
    {
        public List<Order> Orders { get; set; } = new();
        public Dictionary<string, List<OrderItem>> OrderItems { get; set; } = new();
        public string? SelectedStatus { get; set; }
    }

    public class OrderDetailViewModel
    {
        public Order Order { get; set; }
        public List<OrderItem>? Items { get; set; }

        public string? Username { get; set; }
    }
}
