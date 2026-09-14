using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("profileArtwork")]
    public class ProfileArtwork : BaseModel
    {
        [PrimaryKey("ProfArtid")]
        public int ProfArtid { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("UId")]
        public int Uid { get; set; }

        [Column("image")]
        public string Image { get; set; } = string.Empty;
    }
}
