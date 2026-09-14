using ExtensionHelper;
using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.Sales;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System.Security.Claims;
using X.PagedList.Extensions;   


namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class SalesController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly EnumService _enumService;
        private readonly LocalStorageService _storageService;
        private readonly SalesService _salesService;

        public SalesController(SupabaseService supabaseService, EnumService enumService, LocalStorageService storageService, SalesService salesService)
        {
            _supabaseService = supabaseService;
            _enumService = enumService;
            _storageService = storageService;
            _salesService = salesService;
        }

        public async Task<IActionResult> MainSellerCenter()
        {
            var uidValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            var sellerOrders = await _salesService.GetSellerOrders(uid);

            var actualOrders = (sellerOrders).Where(o => o.PaymentStatus == _enumService.ToStringValue(paymentStatus.paid))
                                            .ToList();

            ViewBag.TotalOrders = sellerOrders.Count;
            ViewBag.TotalPaidOrders = actualOrders.Count;
            ViewBag.TotalRevenue = actualOrders.Sum(x => x.TotalAmount);

            var client = _supabaseService.GetClient();
            var sellerProducts = await client
                .From<Product>()
                .Where(p => p.Uid == uid)
                .Get();

            var user = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Get();

            ViewBag.ActiveListings = sellerProducts.Models.Count(p => p.Status == _enumService.ToStringValue(productStatus.available));
            ViewBag.ProfilePic = user.Model.ProfilePic;
            ViewBag.RecentOrders = sellerOrders.Take(5).ToList();

            return View(sellerOrders);
        }


        public async Task<IActionResult> MyProduct(string viewType = "card")
        {
            var client = _supabaseService.GetClient();

            var uidValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            var productViewModels = await _salesService.GetSellerProducts(uid);

            if (Request.IsAjax())
            {
                var partialViewName = viewType == "grid"
                    ? "_MyProductGridView"
                    : "_MyProductCardView";

                return PartialView(partialViewName, productViewModels);
            }

            return View(productViewModels);
        }

        public async Task<IActionResult> ArtistArtworkDetails(string id)
        {
            var client = _supabaseService.GetClient();

            var productResponse = await client
                .From<Product>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, id)
                .Get();

            var product = productResponse.Models.FirstOrDefault();
            if (product == null)
                return NotFound("Product not found");

            var imagesResponse = await client
                .From<ProductImage>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, id)
                .Get();

            var imageGallery = imagesResponse.Models
                .Where(i => !string.IsNullOrEmpty(i.ImagePath))
                .Select(i => new
                {
                    Path = i.ImagePath,
                    IsPrimary = i.IsPrimary
                })
                .ToList();

            var categories = await client.From<Category>().Get();
            var subCategories = await client.From<SubCategory>().Get();

            var category = categories.Models.FirstOrDefault(c => c.CatId == product.CatId);
            var subCategory = subCategories.Models.FirstOrDefault(s => s.SubCatId == product.SubCatId);

            var model = new ArtistProductDetailsViewModel
            {
                PId = product.PId,
                ProductName = product.ProductName,
                Description = product.Description,
                Price = product.Price,
                StockAvailable = product.StockAvailable,
                Status = product.Status,
                CategoryName = category?.CategoryName,
                SubCategoryName = subCategory?.SubCategoryName,
                Images = imageGallery.Select(g => g.Path).ToList(),
                PrimaryImage = imageGallery.FirstOrDefault(g => g.IsPrimary)?.Path
                               ?? imageGallery.FirstOrDefault()?.Path,
                CreatedAt = product.CreatedDate,
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AddProduct()
        {
            var client = _supabaseService.GetClient();

            var categoryResponse = await client.From<Category>().Get();

            var model = new ProductAddEditViewModel
            {
                CategoryList = categoryResponse.Models.Select(c => new SelectListItem
                {
                    Value = c.CatId.ToString(),
                    Text = c.CategoryName
                }).ToList(),

                SubCategoryList = new List<SelectListItem>()
            };

            ViewBag.StatusList = _enumService.GetEnumSelectListItems<productFilterStatus>();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProduct(ProductAddEditViewModel model)
        {
            var client = _supabaseService.GetClient();

            var uidValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            if (!ModelState.IsValid)
            {
                var categoryResponse = await client.From<Category>().Get();
                var subCategoryResponse = await client.From<SubCategory>().Get();

                model.CategoryList = categoryResponse.Models.Select(c => new SelectListItem
                {
                    Value = c.CatId.ToString(),
                    Text = c.CategoryName
                }).ToList();

                model.SubCategoryList = subCategoryResponse.Models.Select(sc => new SelectListItem
                {
                    Value = sc.SubCatId.ToString(),
                    Text = sc.SubCategoryName
                }).ToList();

                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                Console.WriteLine("Errors: " + string.Join(", ", errors));
                ViewBag.StatusList = _enumService.GetEnumSelectListItems<productFilterStatus>();

                return View(model);
            }

            var product = new Product
            {
                Price = model.Price,
                ShippingFee = model.ShippingFee,
                StockAvailable = model.StockAvailable,
                Status = _enumService.ToStringValue(productStatus.available),
                Description = model.Description,
                ProductName = model.Name,
                CatId = model.CatId,
                SubCatId = model.SubCatId,
                Uid = uid,
                CreatedDate = DateTime.UtcNow
            };

            var response = await client.From<Product>().Insert(product);
            if (response.Models == null || !response.Models.Any())
            {
                TempData["Error"] = "Failed to add product. Please try again.";
                return RedirectToAction("AddProduct");
            }

            var newProductId = response.Models.First().PId;

            if (model.Images != null && model.Images.Any())
            {
                int primaryIndex = Convert.ToInt32(Request.Form["PrimaryImageIndex"]);
                int index = 0;

                foreach (var image in model.Images)
                {
                    var savedFileName = await _storageService.SaveAsync(
                        image,
                        "product",
                        LocalStorageService.DefaultAllowedContentTypes
                    );

                    if (savedFileName == null)
                        continue;

                    var imageUrl = _storageService.BuildFileUrl("product", savedFileName);

                    var imageModel = new ProductImage
                    {
                        PId = newProductId,
                        ImagePath = imageUrl,
                        IsPrimary = (index == primaryIndex)
                    };

                    await client.From<ProductImage>().Insert(imageModel);
                    index++;
                }
            }

            TempData["Success"] = "Product added successfully!";
            return RedirectToAction("MyProduct");
        }      

        [HttpGet]
        public async Task<IActionResult> EditProduct(string id)
        {
            var client = _supabaseService.GetClient();

            var productResponse = await client
                .From<Product>()
                .Where(p => p.PId == id)
                .Single();

            var product = productResponse;
            if (product == null) return NotFound();

            var categoryResponse = await client.From<Category>().Get();
            var subCategoryResponse = await client.From<SubCategory>()
                .Where(sc => sc.CatId == product.CatId)
                .Get();

            var imageResponse = await client
                .From<ProductImage>()
                .Where(img => img.PId == id)
                .Get();

            var model = new ProductAddEditViewModel
            {
                ProductId = product.PId,
                Name = product.ProductName,
                Description = product.Description,
                Price = product.Price,
                ShippingFee = product.ShippingFee,
                StockAvailable = product.StockAvailable,
                CatId = product.CatId,
                SubCatId = product.SubCatId,
                Status = product.Status,
                ExistingImages = imageResponse.Models,
                CategoryList = categoryResponse.Models.Select(c => new SelectListItem
                {
                    Value = c.CatId.ToString(),
                    Text = c.CategoryName
                }).ToList(),
                SubCategoryList = subCategoryResponse.Models.Select(sc => new SelectListItem
                {
                    Value = sc.SubCatId.ToString(),
                    Text = sc.SubCategoryName
                }).ToList()
            };

            ViewBag.StatusList = _enumService.GetEnumSelectListItems<productFilterStatus>();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(ProductAddEditViewModel model)
        {
            var client = _supabaseService.GetClient();

            if (!ModelState.IsValid)
            {
                ViewBag.StatusList = _enumService.GetEnumSelectListItems<productFilterStatus>();
                return View(model);
            }

            var product = await client
                .From<Product>()
                .Where(p => p.PId == model.ProductId)
                .Single();

            if (product == null)
                return NotFound();

            product.ProductName = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.ShippingFee = model.ShippingFee;
            product.StockAvailable = model.StockAvailable;
            product.Status = model.Status;

            if (!string.IsNullOrEmpty(Request.Form["NewCatId"]))
                product.CatId = int.Parse(Request.Form["NewCatId"]);

            if (!string.IsNullOrEmpty(Request.Form["NewSubCatId"]))
                product.SubCatId = int.Parse(Request.Form["NewSubCatId"]);

            await client.From<Product>().Update(product);

            var keepImages = Request.Form["KeepImages"].ToList();
            var primaryImage = Request.Form["PrimaryImage"].FirstOrDefault();

            var existingImagesResponse = await client
                .From<ProductImage>()
                .Where(img => img.PId == model.ProductId)
                .Get();

            var existingImages = existingImagesResponse.Models;

            var imagesToDelete = existingImages
                .Where(img => !keepImages.Contains(img.ImagePath))
                .ToList();

            foreach (var img in imagesToDelete)
            {
                await client.From<ProductImage>().Where(x => x.ImagePath == img.ImagePath).Delete();

                try
                {
                    var uri = new Uri(img.ImagePath, UriKind.RelativeOrAbsolute);
                    var imagePath = uri.IsAbsoluteUri ? uri.LocalPath : img.ImagePath;
                    var normalized = imagePath.Replace("\\", "/").TrimStart('/');

                    var folderName = Path.GetDirectoryName(normalized)?.Replace("\\", "/");
                    var fileName = Path.GetFileName(normalized);

                    if (!string.IsNullOrEmpty(folderName) && !string.IsNullOrEmpty(fileName))
                    {
                        _storageService.Delete(folderName, fileName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete file {img.ImagePath}: {ex.Message}");
                }
            }

            if (model.Images != null && model.Images.Any())
            {
                bool noOldImagesLeft = !keepImages.Any();
                bool firstNewImage = true;

                foreach (var image in model.Images)
                {
                    var savedFileName = await _storageService.SaveAsync(
                        image,
                        "product",
                        LocalStorageService.DefaultAllowedContentTypes
                    );

                    if (savedFileName == null)
                        continue;

                    var imageUrl = _storageService.BuildFileUrl("product", savedFileName);

                    bool isPrimaryForThisImage = false;

                    if (!string.IsNullOrEmpty(primaryImage))
                    {
                        if (primaryImage.Equals(image.FileName, StringComparison.OrdinalIgnoreCase))
                            isPrimaryForThisImage = true;

                        if (imageUrl.Contains(primaryImage, StringComparison.OrdinalIgnoreCase))
                            isPrimaryForThisImage = true;
                    }

                    if (noOldImagesLeft && firstNewImage)
                    {
                        isPrimaryForThisImage = true;
                    }

                    var newImage = new ProductImage
                    {
                        PId = model.ProductId,
                        ImagePath = imageUrl,
                        IsPrimary = isPrimaryForThisImage
                    };

                    await client.From<ProductImage>().Insert(newImage);

                    firstNewImage = false;
                }
            }

            var updatedImagesResponse = await client
                .From<ProductImage>()
                .Where(img => img.PId == model.ProductId)
                .Get();

            var updatedImages = updatedImagesResponse.Models;

            bool hasPrimary = updatedImages.Any(x => x.IsPrimary);

            if (!hasPrimary && !string.IsNullOrEmpty(primaryImage))
            {
                var firstMatch = updatedImages.FirstOrDefault(x =>
                    x.ImagePath.Contains(primaryImage, StringComparison.OrdinalIgnoreCase));

                if (firstMatch != null)
                    firstMatch.IsPrimary = true;
            }

            foreach (var img in updatedImages)
            {
                await client.From<ProductImage>()
                    .Where(x => x.ImageId == img.ImageId)
                    .Update(img);
            }

            TempData["Success"] = "Product updated successfully!";
            return RedirectToAction("MyProduct");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            var client = _supabaseService.GetClient();

            var response = await client
                .From<Product>()
                .Where(p => p.PId == id)
                .Set(p => p.Status, _enumService.ToStringValue(productStatus.deleted))
                .Update();

            if (response.Models == null || !response.Models.Any())
            {
                TempData["Error"] = "Failed to delete product.";
            }
            else
            {
                TempData["Success"] = "Product deleted successfully!";
            }

            return RedirectToAction("MyProduct");
        }

        public async Task<IActionResult> MyOrderList(
            string search,
            string filterDate,
            string paymentStatus,
            string orderStatus,
            string deliveryStatus,
            int page = 1)
        {
            var uId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uId))
                return RedirectToAction("Login", "Account");

            var sellerUid = int.Parse(uId);

            var orders = await _salesService.GetSellerOrders(sellerUid);

            if (!string.IsNullOrEmpty(search))
                orders = orders.Where(o =>
                    o.OrderId.Contains(search) ||
                    o.BuyerName.Contains(search)
                ).ToList();

            if (!string.IsNullOrEmpty(filterDate))
                orders = orders.Where(o => o.OrderDate.ToString("yyyy-MM-dd") == filterDate).ToList();

            if (!string.IsNullOrEmpty(paymentStatus))
                orders = orders.Where(o => o.PaymentStatus == paymentStatus).ToList();

            if (!string.IsNullOrEmpty(orderStatus))
                orders = orders.Where(o => o.OrderStatus == orderStatus).ToList();

            if (!string.IsNullOrEmpty(deliveryStatus))
                orders = orders.Where(o => o.DeliveryStatus == deliveryStatus).ToList();


            ViewBag.Search = search;
            ViewBag.FilterDate = filterDate;
            ViewBag.PaymentStatus = paymentStatus;
            ViewBag.OrderStatus = orderStatus;
            ViewBag.DeliveryStatus = deliveryStatus;

            var pagedOrders = orders.ToPagedList(page, 10);

            return View(pagedOrders);
        }

        public async Task<IActionResult> OrderDetails(string oId)
        {
            if (string.IsNullOrEmpty(oId))
            {
                return RedirectToAction("MyOrderList");
            }

            var orderDetails = await _salesService.GetOrderDetails(oId);

            if (orderDetails == null)
            {
                return NotFound($"Order with ID {oId} not found.");
            }

            return View(orderDetails);
        }

        public async Task<IActionResult> SalesDashboard(int? period)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(uidValue);

            int selectedPeriod = period switch
            {
                30 => 30,
                365 => 365,
                _ => 0 // all time
            };

            DateTime cutoff = selectedPeriod == 0
                ? DateTime.MinValue
                : DateTime.UtcNow.AddDays(-selectedPeriod);

            var client = _supabaseService.GetClient();

            var sellerOrders = await _salesService.GetSellerOrders(uid);

            var orders = sellerOrders
                .Where(o => o.PaymentDate >= cutoff)
                .Where(o => o.PaymentStatus == _enumService.ToStringValue(paymentStatus.paid))
                .OrderBy(o => o.OrderDate)
                .ToList();

            var vm = new SalesDashboardViewModel
            {
                Period = selectedPeriod,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                TotalOrders = orders.Count,
                RecentOrders = orders.Take(10).ToList(),
                Dates = orders.Select(o => o.PaymentDate?.ToString("yyyy-MM-dd")).ToList(),
                Totals = orders.Select(o => o.TotalAmount).ToList()
            };

            return View(vm);
        }

        public async Task<IActionResult> ManageCategory()
        {
            var client = _supabaseService.GetClient();

            var categories = await client.From<Category>().Get();
            var subcategories = await client.From<SubCategory>().Get();

            var model = new ManageCategoryVM
            {
                Categories = categories.Models,
                SubCategories = subcategories.Models
            };

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> AddCategory(string categoryName)
        {
            var client = _supabaseService.GetClient();

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                await client.From<Category>().Insert(new Category
                {
                    CategoryName = categoryName
                });
            }

            return RedirectToAction("ManageCategory");
        }


        [HttpPost]
        public async Task<IActionResult> AddSubCategory(int catId, string subCategoryName)
        {
            var client = _supabaseService.GetClient();

            if (catId > 0 && !string.IsNullOrWhiteSpace(subCategoryName))
            {
                await client.From<SubCategory>().Insert(new SubCategory
                {
                    SubCategoryName = subCategoryName,
                    CatId = catId
                });
            }

            return RedirectToAction("ManageCategory");
        }

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

                return Json(subCategories);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading subcategories: {ex.Message}");
                return Json(new { error = "Unable to load subcategories" });
            }

        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(string orderId, string paymentStatus, string deliveryStatus)
        {
            await _salesService.UpdateOrderStatusAsync(orderId, paymentStatus, deliveryStatus);

            return RedirectToAction("MyOrderList");
        }

        [HttpPost]
        public async Task<IActionResult> ApproveRefund(long refundId, string orderId)
        {
            var client = _supabaseService.GetClient();

            var refund = await client
                .From<Refund>()
                .Where(r => r.RefundId == refundId)
                .Single();

            if (refund == null)
            {
                TempData["Error"] = "Refund request not found.";
                return RedirectToAction("OrderDetails", new { oId = orderId });
            }

            refund.RefundStatus = _enumService.ToStringValue(refundStatus.approved);
            refund.ProcessedDate = DateTime.UtcNow;

            await client
                .From<Refund>()
                .Where(r => r.RefundId == refundId)
                .Set(r => r.RefundStatus, refund.RefundStatus)
                .Set(r => r.ProcessedDate, refund.ProcessedDate)
                .Update();

            TempData["Success"] = "Refund request approved successfully.";
            return RedirectToAction("OrderDetails", new { oId = orderId });
        }

        [HttpPost]
        public async Task<IActionResult> RejectRefund(long refundId, string orderId)
        {
            var client = _supabaseService.GetClient();

            var refund = await client
                .From<Refund>()
                .Where(r => r.RefundId == refundId)
                .Single();

            if (refund == null)
            {
                TempData["Error"] = "Refund request not found.";
                return RedirectToAction("OrderDetails", new { oId = orderId });
            }

            refund.RefundStatus = _enumService.ToStringValue(refundStatus.rejected);
            refund.ProcessedDate = DateTime.UtcNow;

            await client
                .From<Refund>()
                .Where(r => r.RefundId == refundId)
                .Set(r => r.RefundStatus, refund.RefundStatus)
                .Set(r => r.ProcessedDate, refund.ProcessedDate)
                .Update();

            TempData["Success"] = "Refund request rejected.";
            return RedirectToAction("OrderDetails", new { oId = orderId });
        }

    }
}
