using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("address")]
    public class Address : BaseModel
    {

        [PrimaryKey("AId")]
        public int AId { get; set; }

        [Column("address_name")]
        public string AddressName { get; set; } = string.Empty;

        [Column("state")]
        public string State { get; set; } = string.Empty;

        [Column("postal_code")]
        public string PostalCode { get; set; } = string.Empty;

        [Column("unit_no")]
        public string UnitNo { get; set; } = string.Empty;

        [Column("is_default")]
        public bool IsDefault { get; set; }
        [Column("UId")]
        public int UId { get; set; }

    }
}
