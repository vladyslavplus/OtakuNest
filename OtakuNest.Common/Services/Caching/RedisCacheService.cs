using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace OtakuNest.Common.Services.Caching
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDatabase _db;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

        public RedisCacheService(IConfiguration configuration, ILogger<RedisCacheService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
            _db = multiplexer.GetDatabase();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            _logger.LogInformation("Connected to Redis at {Connection}", redisConnection);
        }

        public async Task<T?> GetDataAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Cache key is null or empty");
                return default;
            }

            var data = await _db.StringGetAsync(key);
            if (data.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return default;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(data!, _jsonOptions);
        }

        public async Task SetDataAsync<T>(string key, T data, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Cache key is null or empty");
                return;
            }

            if (data is null)
            {
                _logger.LogWarning("Attempting to cache null data for key: {Key}", key);
                return;
            }

            var serializedData = JsonSerializer.Serialize(data, _jsonOptions);
            await _db.StringSetAsync(key, serializedData, expiration ?? DefaultExpiration);

            _logger.LogDebug("Data cached successfully for key: {Key} with expiration: {Expiration}",
                key, expiration ?? DefaultExpiration);
        }

        public async Task RemoveDataAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            await _db.KeyDeleteAsync(key);
            _logger.LogDebug("Cache key removed: {Key}", key);
        }

        public async Task AddToSetAsync(string setKey, string value)
        {
            if (string.IsNullOrWhiteSpace(setKey) || string.IsNullOrWhiteSpace(value)) return;
            await _db.SetAddAsync(setKey, value);
        }

        public async Task<IEnumerable<string>> GetSetMembersAsync(string setKey)
        {
            if (string.IsNullOrWhiteSpace(setKey)) return Enumerable.Empty<string>();
            var members = await _db.SetMembersAsync(setKey);
            return members.Select(m => m.ToString());
        }

        public async Task ClearSetAsync(string setKey)
        {
            if (string.IsNullOrWhiteSpace(setKey)) return;
            await _db.KeyDeleteAsync(setKey);
        }
    }
}