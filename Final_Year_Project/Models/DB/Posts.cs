using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("posts")]
    public class Posts : BaseModel
    {
        [PrimaryKey("post_id", false)]
        public int PostId { get; set; }
        [Column("UId")]
        public int UId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [Column("title")]
        public string? Title { get; set; } = string.Empty;
        [Column("content")]
        public string? Content { get; set; } = string.Empty;
        [Column("status")]
        public string Status { get; set; } = "draft";
        [Column("SPId")]
        public int? SPId { get; set; }
        [Column("cover_image")]
        public string? CoverImage { get; set; }
    }
}
