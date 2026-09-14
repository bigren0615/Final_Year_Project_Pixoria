using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("review")]
    public class Review : BaseModel
    {
        [PrimaryKey("RId")]
        public int Rid { get; set; }

        [Column("Uid")]
        public int UId { get; set; }

        [Column("PId")]
        public string? PId { get; set; }

        [Column("star")]
        public int Star { get; set; }

        [Column("comment")]
        public string? Comment { get; set; }
    }
}
