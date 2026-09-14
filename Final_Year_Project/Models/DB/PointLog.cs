using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("pointLog")]
    public class PointLog : BaseModel
    {
        [PrimaryKey("PLId")]
        public int PLId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("UId")]
        public int UId { get; set; }
        [Column("point")]
        public int Point { get; set; }
        [Column("remark")]
        public string Remark { get; set; } = string.Empty;
    }
}
