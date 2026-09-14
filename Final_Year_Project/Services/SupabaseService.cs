using System.Collections.Concurrent;
using Supabase;
using Microsoft.Extensions.Configuration;

namespace Final_Year_Project.Services
{
    public class SupabaseService
    {
        private readonly Client _client;
        private static readonly ConcurrentDictionary<string, (string Url, DateTime Expiry)> _cache = new();

        public SupabaseService(IConfiguration configuration)
        {
            var url = configuration["Supabase:Url"];
            var anonKey = configuration["Supabase:AnonKey"];

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            _client = new Client(url, anonKey, options);
            _client.InitializeAsync().Wait();
        }

        public Client GetClient() => _client;

        public static implicit operator SupabaseService(Client v)
        {
            throw new NotImplementedException();
        }
    }
}

