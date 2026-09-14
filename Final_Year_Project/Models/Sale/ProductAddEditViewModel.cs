using Final_Year_Project.Models.DB;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.Sales
{
    public class ProductAddEditViewModel
    {
        public string? ProductId { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(1, 999999999)]
        public float Price { get; set; }

        [Required(ErrorMessage = "Shipping Fee is required")]
        [Range(0, 999999999)]
        public float ShippingFee { get; set; }

        [Required(ErrorMessage = "Stock quantity is required")]
        public int StockAvailable { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Main category is required")]
        public int CatId { get; set; }

        [Required(ErrorMessage = "Sub category is required")]
        public int SubCatId { get; set; }

        [Required(ErrorMessage = "Product Status is required")]
        public string? Status { get; set; }

        public IEnumerable<SelectListItem>? CategoryList { get; set; }

        public IEnumerable<SelectListItem>? SubCategoryList { get; set; }

        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public List<IFormFile>? Images { get; set; }

        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public List<ProductImage>? ExistingImages { get; set; }
    }

}
