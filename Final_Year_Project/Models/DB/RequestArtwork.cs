using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("requestArtwork")]
    public class RequestArtwork : BaseModel
    {
        [PrimaryKey("RAId")]
        public int RAId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("img_name")]
        public string ImgName { get; set; } = string.Empty;
        [Column("type")]
        public string Type { get; set; } = string.Empty;
        [Column("RId")]
        public int RId { get; set; }
        [Column("comment")]
        public string? Comment { get; set; }
    }
}
