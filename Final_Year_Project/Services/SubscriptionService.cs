using Final_Year_Project.Models.DB;

namespace Final_Year_Project.Services
{
    public class SubscriptionService
    {
        private readonly SupabaseService _supabaseService;

        public SubscriptionService(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        /// <summary>
        /// Returns the number of active subscribers for the given artist uid.
        /// Implementation: fetches active SubscriptionPlans owned by the artist,
        /// then counts active Subscription rows whose SPId belongs to those plans.
        /// Uses a single WHERE with Contains when supported by the Postgrest client.
        /// </summary>
        public async Task<int> GetSubscriberCountForArtistAsync(int artistUid)
        {
            var client = _supabaseService.GetClient();

            var plansResponse = await client
                .From<SubscriptionPlan>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, artistUid)
                .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "active")
                .Get();

            var planIds = plansResponse.Models.Select(p => p.SPId).ToArray();
            if (planIds.Length == 0)
                return 0;

            // Try to query subscriptions where SPId in planIds and status == active
            try
            {
                var subsResponse = await client
                    .From<Subscription>()
                    .Where(s => planIds.Contains(s.SPId) && s.Status == "active")
                    .Get();

                return subsResponse.Models.Count;
            }
            catch
            {
                // Fallback to per-plan queries if the LINQ Where above isn't supported by client
                var total = 0;
                foreach (var pid in planIds)
                {
                    var resp = await client
                        .From<Subscription>()
                        .Filter("SPId", Supabase.Postgrest.Constants.Operator.Equals, pid)
                        .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "active")
                        .Get();

                    total += resp.Models.Count;
                }

                return total;
            }
        }
    }
}
