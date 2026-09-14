using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Stripe.Checkout;
using Supabase;
using Supabase.Postgrest;

namespace Final_Year_Project.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly SupabaseService _supabaseService;

        public PaymentService(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        public Session CreateCheckoutSession(decimal total, string domain, string successUrl = "Payment/Success", string cancelUrl = "Payment/Cancel")
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)Math.Round(total * 100),
                            Currency = "myr",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Art Store Order"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = domain + successUrl,
                CancelUrl = domain + cancelUrl,
            };

            var service = new SessionService();
            var session = service.Create(options);

            return session;
        }

        public async Task<Order?> CreateOrderFromCartAsync(int uid, decimal total, string paymentStatus, 
            string orderStatus, string deliveryStatus, bool reduceStock = true, DateTime? paymentDateTime = null)
        {
            var client = _supabaseService.GetClient();

            var address = await GetDefaultAddressAsync(uid);

            var order = new Order
            {
                OId = await GenerateOrderIdAsync(),
                Uid = uid,
                OrderDateTime = DateTime.UtcNow,
                PaymentDateTime = paymentDateTime,
                PaymentStatus = paymentStatus,
                DeliveryStatus = deliveryStatus,
                OrderStatus = orderStatus,
                TotalAmount = (float)total,
                Address = address,
                UserVoucherId = null
            };


            var orderResponse = await client
                .From<Order>()
                .Insert(order, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            var createdOrder = orderResponse.Models.FirstOrDefault();
            if (createdOrder == null)
                return null;

            var cartResponse = await client.From<Cart>().Where(c => c.UId == uid).Get();
            var cartItems = cartResponse.Models;

            foreach (var item in cartItems)
            {
                var productResponse = await client
                    .From<Product>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, item.PId)
                    .Get();

                var product = productResponse.Models.FirstOrDefault();
                if (product == null)
                    continue;

                var productImagesResponse = await client.From<ProductImage>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, item.PId)
                    .Get();

                var productImages = productImagesResponse.Models ?? new List<ProductImage>();
                var primaryImage = productImages.FirstOrDefault(i => i.IsPrimary)?.ImagePath ?? "no_image.png";
                var subtotal = (float)((product.Price + product.ShippingFee) * item.Quantity);

                var orderItem = new OrderItem
                {
                    OId = createdOrder.OId,
                    PId = product.PId,
                    Price = (float)product.Price,
                    Unit = item.Quantity,
                    Subtotal = subtotal,
                    ArtworkName = product.ProductName,
                    Image = primaryImage
                };

                if (reduceStock && product != null)
                {
                    var newQuantity = product.StockAvailable - item.Quantity;
                    product.StockAvailable = newQuantity;
                    await client.From<Product>().Update(product);
                }

                await client.From<OrderItem>().Insert(orderItem);
            }

            await client.From<Cart>().Where(c => c.UId == uid).Delete();

            return createdOrder;
        }

        public async Task<string> GetDefaultAddressAsync(int uid)
        {
            var client = _supabaseService.GetClient();

            var addressResponse = await client
                .From<Address>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Get();

            if (!addressResponse.Models.Any())
                return string.Empty;

            var defaultAddress = addressResponse.Models.FirstOrDefault(a => a.IsDefault) ?? addressResponse.Models.First();

            return $"{defaultAddress.UnitNo}, {defaultAddress.AddressName}, {defaultAddress.PostalCode}, {defaultAddress.State}";
        }

        public async Task<string> GenerateOrderIdAsync()
        {
            var client = _supabaseService.GetClient();

            // Count existing orders today
            var today = DateTime.UtcNow.ToString("yyyyMMdd");

            var response = await client
                .From<Order>()
                .Filter("OId", Supabase.Postgrest.Constants.Operator.Like, $"ORD-{today}-%")
                .Get();

            int count = response.Models.Count + 1;

            // Return meaningful order id 
            return $"ORD-{today}-{count.ToString("D6")}";
        }

        public async Task<Order?> CreateSuscriptionAsync(int uid, decimal total, string paymentStatus, bool reduceStock = true, DateTime? paymentDateTime = null)
        {
            var client = _supabaseService.GetClient();

            var address = await GetDefaultAddressAsync(uid);

            var order = new Order
            {
                Uid = uid,
                OrderDateTime = DateTime.UtcNow,
                PaymentDateTime = paymentDateTime,
                PaymentStatus = paymentStatus,
                TotalAmount = (float)total,
                Address = address,
                UserVoucherId = null
            };

            var orderResponse = await client
                .From<Order>()
                .Insert(order, new QueryOptions { Returning = QueryOptions.ReturnType.Representation });

            var createdOrder = orderResponse.Models.FirstOrDefault();
            if (createdOrder == null)
                return null;

            var cartResponse = await client.From<Cart>().Where(c => c.UId == uid).Get();
            var cartItems = cartResponse.Models;

            foreach (var item in cartItems)
            {
                var productResponse = await client
                    .From<Product>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, item.PId)
                    .Get();

                var product = productResponse.Models.FirstOrDefault();
                if (product == null)
                    continue;

                var productImagesResponse = await client.From<ProductImage>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, item.PId)
                    .Get();

                var productImages = productImagesResponse.Models ?? new List<ProductImage>();
                var primaryImage = productImages.FirstOrDefault(i => i.IsPrimary)?.ImagePath ?? "no_image.png";
                var subtotal = (float)((product.Price + product.ShippingFee) * item.Quantity);

                var orderItem = new OrderItem
                {
                    OId = createdOrder.OId,
                    PId = product.PId,
                    Price = (float)product.Price,
                    Unit = item.Quantity,
                    Subtotal = subtotal,
                    ArtworkName = product.ProductName,
                    Image = primaryImage
                };

                if (reduceStock && product != null)
                {
                    var newQuantity = product.StockAvailable - item.Quantity;
                    product.StockAvailable = newQuantity;
                    await client.From<Product>().Update(product);
                }

                await client.From<OrderItem>().Insert(orderItem);
            }

            await client.From<Cart>().Where(c => c.UId == uid).Delete();

            return createdOrder;
        }
    }
}
