using Final_Year_Project.Models.DB;
using X.PagedList;

namespace Final_Year_Project.Models.Product
{
    public class ProductFilterViewModel
    {
        public string? Name { get; set; }
        public int? CatId { get; set; }
        public int? SubCatId { get; set; }

        public IEnumerable<Category> Categories { get; set; } = new List<Category>();
        public IEnumerable<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
        public IPagedList<ProductSalesViewModel> Products { get; set; }
    }
}