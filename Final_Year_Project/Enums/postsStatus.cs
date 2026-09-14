using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Enums
{
    public enum postsStatus
    {
        [Display(Name = "Published")]
        published,

        [Display(Name = "Private")]
        @private,

        [Display(Name = "Draft")]
        draft,

        [Display(Name = "Deleted")]
        deleted
    }
}
