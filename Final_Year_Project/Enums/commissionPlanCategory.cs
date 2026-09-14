using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Enums
{
    public enum commissionPlanCategory
    {
        [Display(Name = "Illustration")]
        illustration,

        [Display(Name = "Character Design")]
        character_design,

        [Display(Name = "Sticker")]
        sticker,

        [Display(Name = "Background Art")]
        background_art
    }
}
