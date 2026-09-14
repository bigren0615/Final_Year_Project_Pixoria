using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.OrderHistory;
using Final_Year_Project.Models.Sales;
using X.PagedList.Extensions;

namespace Final_Year_Project.Services
{
    public class SalesService
    {
        private readonly SupabaseService _supabaseService;
        private readonly EnumService _enumService;

        public SalesService(SupabaseService supabaseService, EnumService enumService)
        {
            _supabaseService = supabaseService;
            _enumService = enumService;
        }

        public async Task<List<SellerOrderViewModel>> GetSellerOrders(int sellerUid)
        {
            var client = _supabaseService.GetClient();

            var productResp = await client
                .From<Product>()
                .Where(p => p.Uid == sellerUid)
                .Get();

            var products = productResp.Models;

            var productIds = products
                .Where(p => !string.IsNullOrEmpty(p.PId))
                .Select(p => p.PId!)
                .ToList();

            if (!productIds.Any())
                return new List<SellerOrderViewModel>();

            var orderItemsResp = await client
                .From<OrderItem>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.In, productIds)
                .Get();

            var orderItems = orderItemsResp.Models;

            var orderIds = orderItems
                .Select(oi => oi.OId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            if (!orderIds.Any())
                return new List<SellerOrderViewModel>();

            var ordersResp = await client
                .From<Order>()
                .Filter("OId", Supabase.Postgrest.Constants.Operator.In, orderIds)
                .Get();

            var orders = ordersResp.Models;

            var buyerUids = orders
                .Select(o => o.Uid)
                .Where(uid => uid != null)
                .Distinct()
                .ToList();

            var buyerResp = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.In, buyerUids)
                .Get();

            var buyers = buyerResp.Models;

            var buyerNameMap = buyers.ToDictionary(
                x => x.Uid,
                x => $"{x.Name}"
            );

            var imagesResponse = await client
                .From<ProductImage>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, productIds)
                .Get();

            var imageGallery = imagesResponse.Models
                .Where(i => !string.IsNullOrEmpty(i.ImagePath))
                .Select(i => new
                {
                    Path = i.ImagePath,
                    IsPrimary = i.IsPrimary
                })
                .ToList();

            var allOrderItems = (
                from o in orders
                join oi in orderItems on o.OId equals oi.OId
                join p in products on oi.PId equals p.PId
                select new SellerOrderViewModel
                {
                    OrderId = o.OId,
                    OrderDate = o.OrderDateTime,
                    PaymentDate = o.PaymentDateTime,
                    TotalAmount = o.TotalAmount, 
                    PaymentStatus = o.PaymentStatus,
                    OrderStatus = o.OrderStatus,
                    DeliveryStatus = o.DeliveryStatus,
                    BuyerName = buyerNameMap[o.Uid]
                }
            ).ToList();

            var groupedOrders = allOrderItems
                .GroupBy(x => x.OrderId)
                .Select(g => new SellerOrderViewModel
                {
                    OrderId = g.Key,
                    OrderDate = g.First().OrderDate,
                    PaymentDate = g.First().PaymentDate,
                    PaymentStatus = g.First().PaymentStatus,
                    DeliveryStatus = g.First().DeliveryStatus,
                    OrderStatus = g.First().OrderStatus,
                    BuyerName = g.First().BuyerName,
                    TotalAmount = g.Sum(x => x.TotalAmount) 
                })
                .OrderByDescending(x => x.OrderDate)
                .ToList();

            return groupedOrders;
        }

        public async Task<SellerOrderViewModel> GetOrderDetails(string oId)
        {
            var client = _supabaseService.GetClient();

            var orderTask = client.From<Order>().Where(o => o.OId == oId).Single();
            var orderItemsTask = client.From<OrderItem>().Where(oi => oi.OId == oId).Get();

            await Task.WhenAll(orderTask, orderItemsTask);

            var order = orderTask.Result;
            var orderItems = orderItemsTask.Result?.Models;

            if (order == null || orderItems == null || !orderItems.Any())
            {
                return null;
            }

            var productIds = orderItems
                .Select(oi => oi.PId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            var productsTask = client.From<Product>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.In, productIds)
                .Get();

            var allImagesTask = client.From<ProductImage>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.In, productIds)
                .Get();

            var buyerTask = client.From<Users>()
                .Where(u => u.Uid == order.Uid)
                .Single();

            var refundsTask = client.From<Refund>()
                .Where(r => r.OId == oId)
                .Get();

            await Task.WhenAll(productsTask, allImagesTask, buyerTask, refundsTask);

            var products = productsTask.Result.Models.ToDictionary(p => p.PId, p => p);
            var allImages = allImagesTask.Result.Models;
            var buyer = buyerTask.Result;
            var refunds = refundsTask.Result.Models;

            var imageGalleryMap = allImages
                .Where(i => !string.IsNullOrEmpty(i.PId))
                .GroupBy(i => i.PId)
                .ToDictionary(
                    g => g.Key!,
                    g => new
                    {
                        Primary = g.FirstOrDefault(i => i.IsPrimary == true)?.ImagePath,
                        AllPaths = g.Select(i => i.ImagePath).Where(p => p != null).ToList()
                    }
                );
            var currentPayment = Enum.Parse<orderPaymentStatus>(order.PaymentStatus);
            var currentOrder = Enum.Parse<orderStatus>(order.OrderStatus);
            var currentDelivery = Enum.Parse<deliveryStatus>(order.DeliveryStatus);


            var viewModel = new SellerOrderViewModel
            {
                OrderId = order.OId,
                OrderDate = order.OrderDateTime,
                PaymentDate = order.PaymentDateTime,
                TotalAmount = order.TotalAmount,
                PaymentStatus = order.PaymentStatus,
                OrderStatus = order.OrderStatus,
                DeliveryStatus = order.DeliveryStatus,
                BuyerName = buyer.Name,
                ShippingAddress = order.Address,
                Items = orderItems.Select(oi => new OrderItemDetail
                {
                    ProductId = oi.PId,
                    ProductName = products.GetValueOrDefault(oi.PId)?.ProductName ?? "Product Not Found",
                    Quantity = oi.Unit,
                    Price = oi.Price,
                    ImagePath = imageGalleryMap.GetValueOrDefault(oi.PId)?.Primary ?? string.Empty,
                    AllImages = imageGalleryMap.GetValueOrDefault(oi.PId)?.AllPaths ?? new List<string>(),
                    OrderItemId = oi.OrderItemId
                }).ToList(),
                AllowedPayment = Allowed(currentPayment),
                AllowedOrder = Allowed(currentOrder),
                AllowedDelivery = AllowedDeliveryForSeller(currentDelivery)
            };

            // Add refund information
            if (refunds.Any())
            {
                var refundIds = refunds.Select(r => r.RefundId).Where(id => id > 0).ToList();
                var refundImagesTask = client.From<RefundImage>()
                    .Filter("refundId", Supabase.Postgrest.Constants.Operator.In, refundIds.Cast<object>().ToList())
                    .Get();

                var refundImages = (await refundImagesTask).Models;

                var refundImageMap = refundImages
                    .Where(ri => ri.RefundId > 0)
                    .GroupBy(ri => ri.RefundId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(ri => ri.ImagePath).Where(p => !string.IsNullOrEmpty(p)).ToList()
                    );

                viewModel.RefundRequests = refunds.Select(r => new RefundDetail
                {
                    RefundId = r.RefundId,
                    OrderItemId = r.OrderItemId,
                    ProductName = orderItems.FirstOrDefault(oi => oi.OrderItemId == r.OrderItemId)?.ArtworkName ?? "Unknown",
                    Reason = r.Reason,
                    RefundStatus = r.RefundStatus,
                    RequestDate = r.RequestDate,
                    DueDate = r.DueDate,
                    Images = refundImageMap.GetValueOrDefault(r.RefundId) ?? new List<string>(),
                    CustomerName = buyer.Name
                }).ToList();
            }

            return viewModel;
        }

        public async Task<IEnumerable<SellerProductViewModel>> GetSellerProducts(int uid)
        {
            var client = _supabaseService.GetClient();

            var productResponse = await client.From<Product>()
                    .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                    .Not("status", Supabase.Postgrest.Constants.Operator.Equals, _enumService.ToStringValue(postsStatus.deleted))
                    .Get();

            var products = productResponse.Models;

            var productIds = products.Select(p => p.PId)
                .Where(p => p != null)
                .ToList();

            var productImages = new List<ProductImage>();

            if (productIds.Any())
            {
                var imageResponse = await client
                    .From<ProductImage>()
                    .Filter("PId", Supabase.Postgrest.Constants.Operator.In, productIds.Cast<object>().ToList())
                    .Get();
                productImages = imageResponse.Models;
            }

            return products.Select(p => new SellerProductViewModel
            {
                ProductId = p.PId,
                ProductName = p.ProductName,
                Description = p.Description,
                Price = p.Price,
                StockAvailable = p.StockAvailable,
                Status = p.Status,
                ImagePath = productImages.FirstOrDefault(i => i.PId == p.PId && i.IsPrimary)?.ImagePath
            }).ToList();
        }

        public async Task UpdateOrderStatusAsync(string orderId, string newPayment, string newDelivery)
        {
            var client = _supabaseService.GetClient();

            var order = await client
                .From<Order>()
                .Where(o => o.OId == orderId)
                .Single();

            if (order == null)
                throw new Exception("Order not found.");

            var curPayment = Enum.Parse<orderPaymentStatus>(order.PaymentStatus);
            var curDelivery = Enum.Parse<deliveryStatus>(order.DeliveryStatus);

            var nextPayment = Enum.Parse<orderPaymentStatus>(newPayment);
            var nextDelivery = Enum.Parse<deliveryStatus>(newDelivery);

            // Prevent sellers from setting delivery status to "delivered"
            if (nextDelivery == deliveryStatus.delivered)
                throw new Exception("Only customers can mark orders as delivered.");

            if (!IsValidTransition(curPayment, nextPayment))
                throw new Exception($"Invalid payment status transition {curPayment} → {nextPayment}");
            if (!IsValidTransition(curDelivery, nextDelivery))
                throw new Exception($"Invalid delivery status transition {curDelivery} → {nextDelivery}");

            order.OrderStatus = GetDerivedOrderStatus(nextPayment, nextDelivery, order.OrderStatus);

            order.PaymentStatus = newPayment;
            order.DeliveryStatus = newDelivery;

            await client
                .From<Order>()
                .Where(o => o.OId == orderId)
                .Set(o => o.PaymentStatus, order.PaymentStatus)
                .Set(o => o.DeliveryStatus, order.DeliveryStatus)
                .Set(o => o.OrderStatus, order.OrderStatus)
                .Update();
        }

        private string GetDerivedOrderStatus(orderPaymentStatus payment, deliveryStatus delivery, string currentOrderStatus)
        {
            return payment switch
            {
                orderPaymentStatus.pending => orderStatus.pending.ToString(),
                orderPaymentStatus.refunded => orderStatus.refunded.ToString(),
                orderPaymentStatus.paid => delivery switch
                {
                    deliveryStatus.pending => orderStatus.confirmed.ToString(),
                    deliveryStatus.packed => orderStatus.processing.ToString(),
                    deliveryStatus.shipped => orderStatus.processing.ToString(),
                    deliveryStatus.delivered => orderStatus.completed.ToString(),
                    deliveryStatus.returned => orderStatus.refunded.ToString(),
                    _ => currentOrderStatus
                },
                _ => currentOrderStatus
            };
        }

        private static readonly Dictionary<Type, Dictionary<Enum, List<Enum>>> _rules =
            new()
            {
                {
                    typeof(orderPaymentStatus),
                    new Dictionary<Enum, List<Enum>>
                    {
                        { orderPaymentStatus.pending,   new(){ orderPaymentStatus.paid, orderPaymentStatus.refunded } },
                        { orderPaymentStatus.paid,      new(){ orderPaymentStatus.refunded } },
                        { orderPaymentStatus.refunded,  new() }
                    }
                },

                {
                    typeof(deliveryStatus),
                    new Dictionary<Enum, List<Enum>>
                    {
                        { deliveryStatus.pending,   new(){ deliveryStatus.packed } },
                        { deliveryStatus.packed,    new(){ deliveryStatus.shipped } },
                        { deliveryStatus.shipped,   new(){ deliveryStatus.delivered } },
                        { deliveryStatus.delivered, new(){ deliveryStatus.returned } },
                        { deliveryStatus.returned,  new() }
                    }
                },

                {
                    typeof(orderStatus),
                    new Dictionary<Enum, List<Enum>>
                    {
                        { orderStatus.pending,     new(){ orderStatus.confirmed, orderStatus.cancelled } },
                        { orderStatus.confirmed,   new(){ orderStatus.processing, orderStatus.cancelled } },
                        { orderStatus.processing,  new(){ orderStatus.completed, orderStatus.cancelled } },
                        { orderStatus.completed,   new(){ orderStatus.refunded } },
                        { orderStatus.cancelled,   new(){ orderStatus.refunded } },
                        { orderStatus.refunded,    new() }
                    }
                }
            };

        public List<string> Allowed<T>(T current) where T : Enum
        {
            var type = typeof(T);
            if (!_rules.ContainsKey(type))
                return new List<string>();

            return _rules[type][current]
                .Select(x => x.ToString())
                .ToList();
        }

        public List<string> AllowedDeliveryForSeller(deliveryStatus current)
        {
            // Get the normal allowed statuses
            var allowed = Allowed(current);
            
            // Remove "delivered" status - only customers can mark as delivered
            allowed.Remove("delivered");
            
            return allowed;
        }

        public bool IsValidTransition<T>(T current, T next) where T : Enum
        {
            // allow no-change
            if (current.Equals(next))
                return true;

            var type = typeof(T);
            if (!_rules.ContainsKey(type))
                return false;

            return _rules[type][current].Contains(next);
        }


    }
}
