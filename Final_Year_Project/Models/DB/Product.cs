using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("product")]
    public class Product : BaseModel
    {
        [PrimaryKey("PId", false)]
        public string? PId { get; set; }

        [Column("name")]
        public string? ProductName { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("CatId")]
        public int CatId { get; set; }

        [Column("subCatId")]
        public int SubCatId { get; set; }

        [Column("price")]
        public float Price { get; set; }

        [Column("stock_available")]
        public int StockAvailable { get; set;}

        [Column("status")]
        public string? Status { get; set; }

        [Column("shippingFee")]
        public float ShippingFee { get; set; } = 0;
        
        [Column("created_date")]
        public DateTime CreatedDate { get; set; }
        [Column("UId")]
        public int? Uid { get; set; }

    }
}
