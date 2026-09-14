using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("tagArt")]
    public class TagArt : BaseModel
    {

        [PrimaryKey("tagArtId")]
        public int TagArtId { get; set; }

        [Column("TagId")]
        public int TagId { get; set; }

        [Column("ArtId")]
        public string? ArtId { get; set; }

    }
}
