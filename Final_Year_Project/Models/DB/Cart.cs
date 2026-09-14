using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("cart")]
    public class Cart : BaseModel
    {

        [Column("Uid")]
        public int UId { get; set; }

        [Column("PId")]
        public string? PId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

    }
}
