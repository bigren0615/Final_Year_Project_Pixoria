using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("tagPost")]
    public class TagPost : BaseModel
    {
        [PrimaryKey("TPId")]
        public int TPId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("post_id")]
        public int PostId { get; set; }
        [Column("tag_id")]
        public int TagId { get; set; }
    }
}
