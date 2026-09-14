
namespace Final_Year_Project.Models.Sales
{
    public class SalesDashboardViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Period { get; set; }

        public List<string> Dates { get; set; } = new();
        public List<float> Totals { get; set; } = new();

        public float TotalRevenue { get; set; }
        public int TotalOrders { get; set; }

        public string? PaymentStatus { get; set; }

        public List<SellerOrderViewModel> RecentOrders { get; set; } = new();
    }


}
