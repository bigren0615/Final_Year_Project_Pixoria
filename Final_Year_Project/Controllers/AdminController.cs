using System.Threading;
using System.Threading.Tasks;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Final_Year_Project.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly SubscriptionExpiryManager _expiryManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AdminController> _logger;

        public AdminController(SubscriptionExpiryManager expiryManager, IWebHostEnvironment env, ILogger<AdminController> logger)
        {
            _expiryManager = expiryManager;
            _env = env;
            _logger = logger;
        }

        [HttpPost]
        [Route("/admin/expire-subscriptions")]
        public async Task<IActionResult> ExpireSubscriptions()
        {
            // Allow manual run in Development (safe for local testing), or for authenticated admin users
            if (!_env.IsDevelopment() && !(User.Identity?.Name != null && User.IsInRole("admin")))
            {
                return Forbid();
            }

            try
            {
                var count = await _expiryManager.ExpireSubscriptionsOnceAsync(CancellationToken.None);
                _logger.LogInformation("AdminController: manually triggered expiry, expired {Count} subscription(s)", count);
                return Json(new { success = true, expiredCount = count });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "AdminController: manual expiry run failed");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
