using Final_Year_Project.Models.OrderHistory;

namespace Final_Year_Project.Models.Product
{
    public class ProductSalesViewModel
    {
        public string? PId { get; set; }                     
        public string? ProductName { get; set; }             
        public string? Description { get; set; }             
        public float Price { get; set; }                    
        public int StockAvailable { get; set; }              
        public string? Status { get; set; }                  
        public decimal ShippingFee { get; set; }
        public string? PrimaryImage { get; set; }           
        public List<string>? ImageGallery { get; set; }
        public int CatId { get; set; }
        public string? CategoryName { get; set; }
        public int SubCatId { get; set; }
        public string? SubCategoryName { get; set; }
        public string? SellerName { get; set; }
        public bool IsInCart { get; set; }
        public List<ReviewViewModel> Reviews { get; set; } = new();
    }
}
