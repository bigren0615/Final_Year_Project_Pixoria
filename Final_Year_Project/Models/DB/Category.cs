using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("category")]
    public class Category : BaseModel
    {

        [PrimaryKey("CatId")]
        public int CatId { get; set; }

        [Column("name")]
        public string? CategoryName { get; set; }
    }

    [Table("subcategory")]
    public class SubCategory : BaseModel
    {

        [PrimaryKey("subCatId")]
        public int SubCatId { get; set; }

        [Column("name")]
        public string? SubCategoryName { get; set; }

        [Column("CatId")]
        public int CatId { get; set; }
    }
}
