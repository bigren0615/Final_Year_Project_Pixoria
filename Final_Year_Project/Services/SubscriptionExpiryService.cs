using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest.Exceptions;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Services
{
    /// <summary>
    /// Background service that scans for subscriptions past their end_date and marks them as expired.
    /// Runs on a configurable interval (seconds) via "SubscriptionExpiry:IntervalSeconds" app setting (default 300s).
    /// </summary>
    public class SubscriptionExpiryService : BackgroundService
    {
        private readonly SubscriptionExpiryManager _manager;
        private readonly ILogger<SubscriptionExpiryService> _logger;
        private readonly int _intervalSeconds;

        public SubscriptionExpiryService(SubscriptionExpiryManager manager, ILogger<SubscriptionExpiryService> logger, IConfiguration config)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _intervalSeconds = config?.GetValue<int?>("SubscriptionExpiry:IntervalSeconds") ?? 300;
            if (_intervalSeconds < 5) _intervalSeconds = 5; // minimum safety
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SubscriptionExpiryService starting (interval={IntervalSeconds}s)", _intervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _manager.ExpireSubscriptionsOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SubscriptionExpiryService: unexpected error during expiry pass");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // graceful shutdown
                    break;
                }
            }

            _logger.LogInformation("SubscriptionExpiryService stopping");
        }

        // The actual expiry logic has been moved to SubscriptionExpiryManager so it can be called
        // from an external controller/action for testing. The hosted service simply delegates to it.
    }
}
