using CommonUtilities.Helpers.GoogleAI;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    public class CustomerServiceController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly IGeminiHelper _geminiHelper;

        public CustomerServiceController(SupabaseService supabaseService, IGeminiHelper geminiHelper)
        {
            _supabaseService = supabaseService;
            _geminiHelper = geminiHelper;
        }


        [HttpGet]
        public async Task<IActionResult> ChatSession()
        {
            var client = _supabaseService.GetClient();

            var uidClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uidClaim == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(uidClaim);

            var response = await client
                .From<ChatbotSession>()
                .Filter("UId", Operator.Equals, uid)
                .Order("id", Ordering.Descending)
                .Get();

            var sessions = response.Models;

            return View(sessions);
        }


        [HttpPost]
        public async Task<IActionResult> CreateSession(string title)
        {
            var client = _supabaseService.GetClient();
            var uidClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uidClaim == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(uidClaim);

            var session = new ChatbotSession
            {
                Title = title,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                UId = uid
            };

            await client.From<ChatbotSession>().Insert(session);

            return RedirectToAction("ChatSession");
        }

        [HttpGet]
        public async Task<IActionResult> ChatMessage(int sessionId)
        {
            var client = _supabaseService.GetClient();

            var uidClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uidClaim == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(uidClaim);

            // Validate ownership
            var session = await client.From<ChatbotSession>()
                .Filter("id", Operator.Equals, sessionId)
                .Filter("UId", Operator.Equals, uid)
                .Single();

            if (session == null)
                return RedirectToAction("ChatSession");

            var response = await client
                .From<ChatbotMessage>()
                .Filter("chatSessionId", Operator.Equals, sessionId)
                .Order("created_at", Ordering.Ascending)
                .Get();

            var messages = response.Models;

            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int sessionId, string sender, string content)
        {
            var client = _supabaseService.GetClient();
            var uidClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uidClaim == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(uidClaim);

            // Save user message
            await client.From<ChatbotMessage>().Insert(new ChatbotMessage
            {
                Sender = "user",
                Content = content,
                CreatedAt = DateTime.UtcNow,
                ChatSessionId = sessionId
            });

            string botReply;

            if (IsOrderStatusQuery(content))
            {
                botReply = await HandleOrderQuery(uid, content, client);
            }
            else if (IsProductRecommendationQuery(content))
            {
                botReply = await HandleProductRecommendationQuery(client);
            }
            else
            {
                try
                {
                    botReply = await _geminiHelper.SendChatMessageAsync(content);
                }
                catch
                {
                    botReply = "Error contacting AI service.";
                }
            }


            await client.From<ChatbotMessage>().Insert(new ChatbotMessage
            {
                Sender = "bot",
                Content = botReply,
                CreatedAt = DateTime.UtcNow,
                ChatSessionId = sessionId
            });

            return Json(new
            {
                user = content,
                bot = botReply,
                timestamp = DateTime.UtcNow.ToString("g")
            });
        }

        private async Task<string> HandleOrderQuery(int uid, string userMessage, Supabase.Client client)
        {
            var orderResponse = await client
                .From<Order>()
                .Where(o => o.Uid == uid)
                .Order("orderDateTime", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            var order = orderResponse.Models.FirstOrDefault();

            if (order == null)
                return "You have no orders.";

            return $"Your latest order ({order.OId}) is currently:\n" +
                   $"- Order Status: {order.OrderStatus}\n" +
                   $"- Payment Status: {order.PaymentStatus}\n" +
                   $"- Delivery Status: {order.DeliveryStatus}\n" +
                   $"Placed on {order.OrderDateTime:dd/MM/yyyy}.";
        }

        private async Task<string> HandleProductRecommendationQuery(Supabase.Client client)
        {
            var response = await client
                .From<Product>()
                .Where(p => p.StockAvailable > 0)
                .Limit(5)
                .Get();

            var products = response.Models;

            if (products.Count == 0)
                return "Sorry, no products are available right now.";

            string result = "Here are some products you might be interested in:\n\n";

            foreach (var p in products)
            {
                result += $"• {p.ProductName} - RM {p.Price}\n";
            }

            return result;
        }


        private bool IsOrderStatusQuery(string msg)
        {
            msg = msg.ToLower();

            return msg.Contains("order status")
                || msg.Contains("track order")
                || msg.Contains("where is my order")
                || msg.Contains("delivery status")
                || msg.Contains("payment status");
        }

        private bool IsProductRecommendationQuery(string msg)
        {
            msg = msg.ToLower();

            return msg.Contains("recommend")
                || msg.Contains("suggest")
                || msg.Contains("show products")
                || msg.Contains("what do you sell")
                || msg.Contains("what products")
                || msg.Contains("show me")
                || msg.Contains("any product");
        }

    }
}
