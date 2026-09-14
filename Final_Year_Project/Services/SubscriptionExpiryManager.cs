using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest.Exceptions;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Services
{
    /// <summary>
    /// Extracted manager for performing a single expiry pass. This allows the background worker
    /// and external callers (e.g. admin endpoints) to reuse the same logic.
    /// </summary>
    public class SubscriptionExpiryManager
    {
        private readonly SupabaseService _supabaseService;
        private readonly EnumService _enumService;
        private readonly ILogger<SubscriptionExpiryManager> _logger;

        public SubscriptionExpiryManager(SupabaseService supabaseService, EnumService enumService, ILogger<SubscriptionExpiryManager> logger)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _enumService = enumService ?? throw new ArgumentNullException(nameof(enumService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs a single pass that finds subscriptions whose end_date is in the past and marks them expired.
        /// </summary>
        public async Task<int> ExpireSubscriptionsOnceAsync(CancellationToken cancellationToken = default)
        {
            var client = _supabaseService.GetClient();
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");

            // Find active subscriptions whose end_date is <= now
            var resp = await client
                .From<Subscription>()
                .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                .Filter("end_date", Operator.LessThanOrEqual, now)
                .Get(cancellationToken);

            var subs = resp.Models?.ToList() ?? new();
            if (!subs.Any())
            {
                _logger.LogDebug("SubscriptionExpiryManager: no expired subscriptions found");
                return 0;
            }

            _logger.LogInformation("SubscriptionExpiryManager: expiring {Count} subscription(s)", subs.Count);

            var count = 0;
            foreach (var sub in subs)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    sub.Status = _enumService.ToStringValue(subscriptionStatus.expired);
                    sub.IsRenewal = false;
                    await client.From<Subscription>().Where(s => s.SId == sub.SId).Update(sub, cancellationToken: cancellationToken);
                    _logger.LogInformation("SubscriptionExpiryManager: expired SId={SId}", sub.SId);
                    count++;
                }
                catch (PostgrestException pex) when (pex.Message != null && pex.Message.Contains("invalid input value for enum", StringComparison.OrdinalIgnoreCase))
                {
                    // try upper-case enum value for Postgres enum mismatch
                    try
                    {
                        sub.Status = _enumService.ToStringValue(subscriptionStatus.expired).ToUpperInvariant();
                        await client.From<Subscription>().Where(s => s.SId == sub.SId).Update(sub, cancellationToken: cancellationToken);
                        _logger.LogInformation("SubscriptionExpiryManager: expired SId={SId} (upper-case retry)", sub.SId);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "SubscriptionExpiryManager: failed to expire SId={SId} on retry", sub?.SId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SubscriptionExpiryManager: failed to expire SId={SId}", sub?.SId);
                }
            }

            return count;
        }
    }
}
