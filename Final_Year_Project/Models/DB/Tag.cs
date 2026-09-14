using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("tag")]
    public class Tag : BaseModel
    {

        [PrimaryKey("tag_id")]
        public int TagId { get; set; }

        [Column("tag_name")]
        public string? TagName { get; set; }

        [Column("status")]
        public string? Status { get; set; } = "available";

    }
}
