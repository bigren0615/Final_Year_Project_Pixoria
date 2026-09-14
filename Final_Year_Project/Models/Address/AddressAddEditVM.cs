using Final_Year_Project.Enums;
using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.Address
{
    public class AddressAddEditVM
    {
        [Required(ErrorMessage = "Address name is required")]
        public string AddressName { get; set; } = string.Empty;

        [Required(ErrorMessage = "State is required")]
        public StateCategory State { get; set; }

        [Required(ErrorMessage = "Postal Code is required")]
        public string PostalCode { get; set; } = string.Empty;
        public string? UnitNo { get; set; }
        public bool IsDefault { get; set; } = false;
    }
}
