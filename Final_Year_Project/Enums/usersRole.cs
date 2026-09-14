using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Enums
{
    public enum usersRole
    {
        [Display(Name = "Artist")]
        artist,

        [Display(Name = "Customer")]
        customer,

        [Display(Name = "Admin")]
        admin,
    }
}
