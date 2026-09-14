using ExtensionHelper;
using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.OrderHistory;
using Final_Year_Project.Models.Product;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supabase.Gotrue;
using System.Security.Claims;
using X.PagedList.Extensions;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class PurchaseController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly EnumService _enumService;

        public PurchaseController(SupabaseService supabaseService, EnumService enumService)
        {
            _supabaseService = supabaseService;
            _enumService = enumService;
        }

        public async Task<IActionResult> PurchasePage(string? name, int? catId, int? subCatId, int page = 1)
        {
            var client = _supabaseService.GetClient();

            var productsResponse = await client.From<Product>().Get();
            var productImagesResponse = await client.From<ProductImage>().Get();
            var categoriesResponse = await client.From<Category>().Get();
            var subCategoriesResponse = await client.From<SubCategory>().Get();

            var products = productsResponse.Models ?? new List<Product>();
            var productImages = productImagesResponse.Models;
            var categories = categoriesResponse.Models ?? new List<Category>();
            var subCategories = subCategoriesResponse.Models ?? new List<SubCategory>();

            var filtered = products
                .Where(p => !string.IsNullOrWhiteSpace(p.Status) &&
                            p.Status.Trim().Equals("available", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(name))
                filtered = filtered.Where(p => p.ProductName.Contains(name, StringComparison.OrdinalIgnoreCase));

            if (catId.HasValue)
                filtered = filtered.Where(p => p.CatId == catId.Value);

            if (subCatId.HasValue)
                filtered = filtered.Where(p => p.SubCatId == subCatId.Value);

            var productViewModels = filtered.Select(p =>
            {
                var primaryImage = productImages.FirstOrDefault(i => i.PId == p.PId && i.IsPrimary)?.ImagePath;
                return new ProductSalesViewModel
                {
                    PId = p.PId,
                    ProductName = p.ProductName,
                    PrimaryImage = primaryImage,
                    Price = p.Price
                };
            }).ToList();

            var pagedProducts = productViewModels.ToPagedList(page, 8);

            var model = new ProductFilterViewModel
            {
                Name = name,
                CatId = catId,
                SubCatId = subCatId,
                Categories = categories,
                SubCategories = subCategories,
                Products = pagedProducts
            };

            if (Request.IsAjax())
                return PartialView("_PurchaseSelection", pagedProducts);

            return View(model);
        }

        public async Task<IActionResult> ViewArtworkDetails(string id)
        {
            var client = _supabaseService.GetClient();

            var productResponse = await client
                .From<Product>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, id)
                .Get();

            var product = productResponse.Models.FirstOrDefault();
            if (product == null)
                return NotFound("Product not found");

            var productImagesResponse = await client
                .From<ProductImage>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, id)
                .Get();

            var productImages = productImagesResponse.Models ?? new List<ProductImage>();

            string sellerName = "Unknown Seller";
            var sellerResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, product.Uid)
                .Single();

            if (sellerResponse != null)
                sellerName = sellerResponse.Username ?? "Unknown Seller";

            var categoriesResponse = await client.From<Category>().Get();
            var subCategoriesResponse = await client.From<SubCategory>().Get();

            var category = categoriesResponse.Models.FirstOrDefault(c => c.CatId == product.CatId);
            var subCategory = subCategoriesResponse.Models.FirstOrDefault(s => s.SubCatId == product.SubCatId);

            var imageGallery = productImages
                .Where(i => !string.IsNullOrEmpty(i.ImagePath))
                .Select(i => i.ImagePath)
                .ToList();

            bool isInCart = false;
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(uidValue))
            {
                var cartResponse = await client
                    .From<Cart>()
                    .Filter("Uid", Supabase.Postgrest.Constants.Operator.Equals, int.Parse(uidValue))
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, id)
                    .Get();

                isInCart = cartResponse.Models.Any();
            }

            var model = new ProductSalesViewModel
            {
                PId = product.PId,
                ProductName = product.ProductName,
                Description = product.Description,
                Price = product.Price,
                StockAvailable = product.StockAvailable,
                SellerName = sellerName,
                ImageGallery = imageGallery,
                CategoryName = category?.CategoryName,
                SubCategoryName = subCategory?.SubCategoryName,
                IsInCart = isInCart,
                Reviews = new List<ReviewViewModel>()
            };

            var reviewResponse = await client
                .From<Review>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, id)
                .Get();

            var reviews = reviewResponse.Models;

            var reviewerIds = reviews.Select(r => r.UId).Distinct().ToList();

            var reviewerResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.In, reviewerIds)
                .Get();

            var reviewers = reviewerResponse.Models ?? new List<Users>();

            model.Reviews = reviews.Select(r =>
            {
                var reviewer = reviewers.FirstOrDefault(u => u.Uid == r.UId);
                return new ReviewViewModel
                {
                    Username = reviewer?.Username ?? "Unknown User",
                    Star = r.Star,
                    Comment = r.Comment
                };
            }).ToList();

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(string PId, int quantity)
        {
            var client = _supabaseService.GetClient();

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue)) 
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);

            if (string.IsNullOrEmpty(PId))
            {
                TempData["Error"] = "Product ID is missing.";
                return RedirectToAction("ViewArtworkDetails", "Purchase");
            }

            var productResponse = await client
                .From<Product>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, PId)
                .Get();

            var product = productResponse.Models.FirstOrDefault();
            if (product == null)
                return NotFound("Product not found");

            if (product.StockAvailable <= 0 || product.Status == _enumService.ToStringValue(productStatus.outOfStock))
            {
                TempData["Error"] = "This Product Out Of Stock";
                return RedirectToAction("ViewArtworkDetails", new { id = PId });
            }

            var existingCartResponse = await client
                .From<Cart>()
                .Filter("Uid", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, PId)
                .Get();

            var existingCartItem = existingCartResponse.Models.FirstOrDefault();

            if (existingCartItem != null)
            {
                existingCartItem.Quantity += quantity;
                await client.From<Cart>().Update(existingCartItem);
            }
            else
            {
                if (quantity <= 0)
                {
                    TempData["Error"] = "Invalid quantity. Must be at least 1.";
                }
                else if (quantity > product.StockAvailable)
                {
                    TempData["Error"] = $"Only {product.StockAvailable} items available.";
                }
                else
                {
                    var newCartItem = new Cart
                    {
                        UId = uid,
                        PId = PId,
                        Quantity = quantity
                    };

                    await client.From<Cart>().Insert(newCartItem);
                    TempData["Success"] = "Item added to cart!";
                }
            }
            return RedirectToAction("ViewArtworkDetails", new { id = PId });
        }

        [HttpGet]
        public async Task<IActionResult> GetSubCategories(int catId)
        {
            try
            {
                var client = _supabaseService.GetClient();

                var response = await client
                    .From<SubCategory>()
                    .Where(sc => sc.CatId == catId)
                    .Get();

                var subCategories = response.Models.Select(sc => new
                {
                    value = sc.SubCatId,
                    text = sc.SubCategoryName
                });

                Console.WriteLine($"Loaded {response.Models.Count} subcategories for CatId={catId}");

                return Json(subCategories);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading subcategories: {ex.Message}");
                return Json(new { error = "Unable to load subcategories" });
            }
        }
    }
}
