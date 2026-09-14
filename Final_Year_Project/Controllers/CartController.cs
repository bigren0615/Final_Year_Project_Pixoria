using Final_Year_Project.Models.Cart;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supabase.Gotrue;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class CartController : Controller
    {
        private readonly SupabaseService _supabaseService;

        public CartController(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        [HttpGet]
        public async Task<IActionResult> ViewCart()
        {
            var client = _supabaseService.GetClient();
            var cartItems = new List<CartViewModel>();

            var uidValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            var cartResponse = await client
                .From<Cart>()
                .Filter("Uid", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Get();

            foreach (var cart in cartResponse.Models)
            {
                var productResponse = await client
                    .From<Product>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, cart.PId)
                    .Single();

                var productImageResponse = await client
                    .From<ProductImage>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, cart.PId)
                    .Filter("is_primary", Operator.Equals, "true")
                    .Single();

                if (productImageResponse == null)
                {
                    var fallbackImageResponse = await client
                        .From<ProductImage>()
                        .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, cart.PId)
                        .Limit(1)
                        .Single();

                    productImageResponse = fallbackImageResponse;
                }

                cartItems.Add(new CartViewModel
                {
                    PId = cart.PId,
                    ArtworkName = productResponse.ProductName,
                    Image = productImageResponse.ImagePath,
                    Price = productResponse.Price,
                    StockAvailable = productResponse.StockAvailable,
                    ShippingFee = productResponse.ShippingFee,
                    Quantity = cart.Quantity
                });
            }

            return View(cartItems);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateItem(string pid, int quantity)
        {
            var client = _supabaseService.GetClient();
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            var productResponse = await client
                .From<Product>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, pid)
                .Get();

            var product = productResponse.Models.FirstOrDefault();
            if (product == null)
                return NotFound("Product not found");

            if (quantity > product.StockAvailable)
            {
                TempData["Error"] = $"Only {product.StockAvailable} items available.";
                return RedirectToAction("ViewCart");
            }
            else if (quantity <=  0)
            {
                TempData["Error"] = "Invalid quantity. Must be at least 1.";
                return RedirectToAction("ViewCart");
            }

                await client
                    .From<Cart>()
                    .Where(c => c.UId == uid && c.PId == pid)
                    .Set(c => c.Quantity, quantity)
                    .Update();

            TempData["Success"] = "Cart item updated successfully.";
            return RedirectToAction("ViewCart");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteItem(string pid)
        {
            var client = _supabaseService.GetClient();
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            await client
                .From<Cart>()
                .Filter("Uid", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, pid)
                .Delete();

            TempData["Success"] = "Item removed from cart.";
            return RedirectToAction("ViewCart");
        }

        [HttpGet]
        public async Task<IActionResult> ViewCheckout()
        {
            var client = _supabaseService.GetClient();
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);
            var userResponse = await client
                    .From<Users>()
                    .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                    .Single();

            var cartResponse = await client
                .From<Cart>()
                .Filter("Uid", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Get();

            if (!cartResponse.Models.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("ViewCart");
            }

            var cartItems = new List<CartViewModel>();

            foreach (var cart in cartResponse.Models)
            {
                var productResponse = await client
                   .From<Product>()
                   .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, cart.PId)
                   .Single();

                var productImageResponse = await client
                    .From<ProductImage>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, cart.PId)
                    .Filter("is_primary", Operator.Equals, "true")
                    .Single();

                if (productImageResponse == null)
                {
                    var fallbackImageResponse = await client
                        .From<ProductImage>()
                        .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, cart.PId)
                        .Limit(1)
                        .Single();

                    productImageResponse = fallbackImageResponse;
                }

                cartItems.Add(new CartViewModel
                {
                    PId = cart.PId,
                    ArtworkName = productResponse.ProductName,
                    Image = productImageResponse.ImagePath,
                    Price = productResponse.Price,
                    StockAvailable = productResponse.StockAvailable,
                    ShippingFee = productResponse.ShippingFee,
                    Quantity = cart.Quantity
                });
            }

            var addressResponse = await client
                .From<Address>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Get();

            string fullAddress = "";
            if (addressResponse.Models.Any())
            {
                var defaultAddress = addressResponse.Models.FirstOrDefault(a => a.IsDefault)
                                     ?? addressResponse.Models.First();

                fullAddress = $"{defaultAddress.UnitNo}, {defaultAddress.AddressName}, {defaultAddress.PostalCode}, {defaultAddress.State}";
            }

            var viewModel = new CheckoutViewModel
            {
                Username = userResponse.Username,
                PhoneNumber = userResponse.PhoneNumber,
                Address = fullAddress,
                CartItems = cartItems
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyVoucher(string voucherCode)
        {
            // Voucher validation logic here (similar to PHP version)
            // e.g., check expiry, type, min spend, etc.
            // Set ViewBag.Message or ModelState error if invalid.

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CancelVoucher()
        {
            HttpContext.Session.Remove("VoucherCode");
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult ProceedToPayment()
        {
            // Save order, deduct stock, redirect to payment gateway page
            return RedirectToAction("Payment", "Order");
        }
    }
}
