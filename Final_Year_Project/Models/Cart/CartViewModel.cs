using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Final_Year_Project.Models.Cart
{
    public class CartViewModel
    {
        public string? PId { get; set; }
        public string? ArtworkName { get; set; }
        public string? Image { get; set; }
        public int StockAvailable { get; set; }
        public float Price { get; set; }
        public int Quantity { get; set; }
        public float ShippingFee { get; set; }
    }
}
