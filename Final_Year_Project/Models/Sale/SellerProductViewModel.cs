namespace Final_Year_Project.Models.Sales
{
    public class SellerProductViewModel
    {
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public float Price { get; set; }
        public int StockAvailable { get; set; }
        public string? Status { get; set; }
        public string? ImagePath { get; set; }
    }
}
