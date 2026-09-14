using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("productImage")]
    public class ProductImage : BaseModel
    {

        [PrimaryKey("ImageId")]
        public int ImageId { get; set; }

        [Column("PId")]
        public string? PId { get; set; }

        [Column("image_path")]
        public string? ImagePath { get; set; }

        [Column("is_primary")]
        public bool IsPrimary { get; set; }
    }
}
