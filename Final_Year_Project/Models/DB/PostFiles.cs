using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("postFiles")]
    public class PostFiles : BaseModel
    {
        [PrimaryKey("file_id", false)]
        public int FileId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("post_id")]
        public int? PostId { get; set; }
        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;
        [Column("type")]
        public string Type { get; set; } = string.Empty;
        [Column("UId")]
        public int UId { get; set; }
        [Column("is_linked")]
        public bool IsLinked { get; set; }
    }
}
