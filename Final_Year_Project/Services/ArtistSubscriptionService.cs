using Final_Year_Project.Models.DB;
using Microsoft.Extensions.Caching.Memory;

namespace Final_Year_Project.Services
{
    public class ArtistSubscriptionService
    {
        private readonly SupabaseService _supabaseService;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        public ArtistSubscriptionService(SupabaseService supabaseService, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _supabaseService = supabaseService;
            _cache = cache;
        }

        public async Task<int> GetSubscriberCountForArtistAsync(int artistUid)
        {
            // Cache key per artist uid
            var cacheKey = $"artist_sub_count_{artistUid}";
            if (_cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is int cached)
                return cached;

            var client = _supabaseService.GetClient();

            var plansResponse = await client
                .From<SubscriptionPlan>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, artistUid)
                .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "active")
                .Get();

            var planIds = plansResponse.Models.Select(p => p.SPId).ToArray();
            if (planIds.Length == 0)
                return 0;

            try
            {
                var subsResponse = await client
                    .From<Subscription>()
                    .Where(s => planIds.Contains(s.SPId) && s.Status == "active")
                    .Get();

                var count = subsResponse.Models.Count;
                _cache.Set(cacheKey, count, TimeSpan.FromSeconds(30));
                return count;
            }
            catch
            {
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

                _cache.Set(cacheKey, total, TimeSpan.FromSeconds(30));
                return total;
            }
        }
    }
}
