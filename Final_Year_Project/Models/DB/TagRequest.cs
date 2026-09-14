using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("tagRequest")]
    public class TagRequest : BaseModel
    {
        [PrimaryKey("TRId")]
        public int TRId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("tag_id")]
        public int TagId { get; set; }
        [Column("RId")]
        public int RId { get; set; }
    }
}
