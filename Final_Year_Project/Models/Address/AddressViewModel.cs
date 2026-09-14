namespace Final_Year_Project.Models.Address
{
    public class AddressViewModel
    {
        public int AId { get; set; }
        public string AddressName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string? UnitNo { get; set; }
        public bool IsDefault { get; set; }
    }
}
