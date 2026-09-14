namespace Final_Year_Project.Models.Sales
{
    public class ArtistProductDetailsViewModel
    {
        public string? PId { get; set; }
        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public float Price { get; set; }
        public int StockAvailable { get; set; }
        public string? Status { get; set; }

        public string? CategoryName { get; set; }
        public string? SubCategoryName { get; set; }

        public string? PrimaryImage { get; set; }
        public List<string>? Images { get; set; }

        public DateTime? CreatedAt { get; set; }
    }

}
