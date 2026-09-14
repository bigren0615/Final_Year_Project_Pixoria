using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("follow")]
    public class Follow : BaseModel
    {
        [PrimaryKey("FId")]
        public int FId { get; set; }
        [Column("f_date")]
        public DateTime FDate { get; set; }
        [Column("follower_id")]
        public int FollowerId { get; set; }
        [Column("artist_id")]
        public int ArtistId { get; set; }
    }
}
