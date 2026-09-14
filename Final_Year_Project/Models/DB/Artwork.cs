using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("artwork")]
    public class Artwork : BaseModel
    {

        [PrimaryKey("ArtId")]
        public string? ArtId { get; set; }

        [Column("UId")]
        public int UId { get; set; }

        [Column("artwork_name")]
        public string? ArtworkName { get; set;}

        [Column("image")]
        public string? Image { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("CatId")]
        public int CatId { get; set; }

        [Column("created_date")]
        public DateTime CreatedDate { get; set; }

    }
}
