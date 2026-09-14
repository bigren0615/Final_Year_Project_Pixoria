using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("postLike")]
    public class PostLike : BaseModel
    {
        [PrimaryKey("PLId", false)]
        public int PLId { get; set; }
        [Column("post_id")]
        public int PostId { get; set; }
        [Column("UId")]
        public int UId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
