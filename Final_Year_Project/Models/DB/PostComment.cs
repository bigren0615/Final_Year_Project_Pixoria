using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("postComment")]
    public class PostComment : BaseModel
    {
        [PrimaryKey("comment_id", false)]
        public int CommentId { get; set; }
        [Column("post_id")]
        public int PostId { get; set; }
        [Column("UId")]
        public int UId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [Column("comment")]
        public string Comment { get; set; } = string.Empty;
        [Column("status")]
        public string Status { get; set; } = string.Empty;
        [Column("parent_id")]
        public int? ParentId { get; set; }
    }
}
