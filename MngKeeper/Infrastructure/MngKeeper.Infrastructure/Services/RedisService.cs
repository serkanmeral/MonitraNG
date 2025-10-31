using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MngKeeper.Application.Interfaces;
using System.Text.Json;
using System.IO.Compression;
using System.Text;

namespace MngKeeper.Infrastructure.Services
{
    public class RedisService : IRedisService, IDisposable
    {
        private readonly ILogger<RedisService> _logger;
        private readonly IConfiguration _configuration;
        private readonly CacheOptions _options;
        private IConnectionMultiplexer? _redis;
        private IDatabase? _database;

        public RedisService(ILogger<RedisService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _options = new CacheOptions();
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_redis?.IsConnected == true)
                {
                    _logger.LogInformation("Redis connection already established");
                    return;
                }

                var connectionString = _configuration["ConnectionStrings:Redis"] ?? "localhost:6379";
                _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
                _database = _redis.GetDatabase();

                _logger.LogInformation("Redis connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_redis?.IsConnected == true)
                {
                    await _redis.CloseAsync();
                }
                _redis?.Dispose();

                _logger.LogInformation("Redis connection closed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing Redis connection");
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            return _redis?.IsConnected == true && _database != null;
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                await EnsureConnectionAsync();

                var fullKey = GetFullKey(key);
                var jsonValue = JsonSerializer.Serialize(value);
                var finalValue = _options.EnableCompression ? Compress(jsonValue) : jsonValue;

                return await _database!.StringSetAsync(fullKey, finalValue, expiry ?? _options.DefaultExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set value for key: {Key}", key);
                return false;
            }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                await EnsureConnectionAsync();

                var fullKey = GetFullKey(key);
                var value = await _database!.StringGetAsync(fullKey);

                if (!value.HasValue)
                    return default;

                var stringValue = value.ToString();
                var jsonValue = _options.EnableCompression ? Decompress(stringValue) : stringValue;

                return JsonSerializer.Deserialize<T>(jsonValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get value for key: {Key}", key);
                return default;
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.KeyDeleteAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete key: {Key}", key);
                return false;
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.KeyExistsAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check existence for key: {Key}", key);
                return false;
            }
        }

        public async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.KeyExpireAsync(fullKey, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set expiry for key: {Key}", key);
                return false;
            }
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.KeyTimeToLiveAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get TTL for key: {Key}", key);
                return null;
            }
        }

        public async Task<long> IncrementAsync(string key, long value = 1)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.StringIncrementAsync(fullKey, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to increment key: {Key}", key);
                return 0;
            }
        }

        public async Task<long> DecrementAsync(string key, long value = 1)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.StringDecrementAsync(fullKey, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrement key: {Key}", key);
                return 0;
            }
        }

        public async Task<bool> SetHashAsync(string key, string field, string value)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.HashSetAsync(fullKey, field, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set hash field: {Key}:{Field}", key, field);
                return false;
            }
        }

        public async Task<string?> GetHashAsync(string key, string field)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var value = await _database!.HashGetAsync(fullKey, field);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get hash field: {Key}:{Field}", key, field);
                return null;
            }
        }

        public async Task<Dictionary<string, string>> GetHashAllAsync(string key)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var hashEntries = await _database!.HashGetAllAsync(fullKey);
                
                return hashEntries.ToDictionary(
                    entry => entry.Name.ToString(),
                    entry => entry.Value.ToString()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all hash fields for key: {Key}", key);
                return new Dictionary<string, string>();
            }
        }

        public async Task<bool> DeleteHashAsync(string key, string field)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                return await _database!.HashDeleteAsync(fullKey, field);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete hash field: {Key}:{Field}", key, field);
                return false;
            }
        }

        public async Task<bool> SetListAsync<T>(string key, List<T> values, TimeSpan? expiry = null)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                
                // Clear existing list
                await _database!.KeyDeleteAsync(fullKey);
                
                // Add all values
                var tasks = values.Select(value => 
                {
                    var jsonValue = JsonSerializer.Serialize(value);
                    return _database.ListRightPushAsync(fullKey, jsonValue);
                });
                
                await Task.WhenAll(tasks);
                
                // Set expiry if specified
                if (expiry.HasValue)
                {
                    await _database.KeyExpireAsync(fullKey, expiry.Value);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set list for key: {Key}", key);
                return false;
            }
        }

        public async Task<List<T>> GetListAsync<T>(string key)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var values = await _database!.ListRangeAsync(fullKey);
                
                var result = new List<T>();
                foreach (var value in values)
                {
                    if (value.HasValue)
                    {
                        var item = JsonSerializer.Deserialize<T>(value.ToString());
                        if (item != null)
                            result.Add(item);
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get list for key: {Key}", key);
                return new List<T>();
            }
        }

        public async Task<bool> PushToListAsync<T>(string key, T value)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var jsonValue = JsonSerializer.Serialize(value);
                var length = await _database!.ListRightPushAsync(fullKey, jsonValue);
                return length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push to list for key: {Key}", key);
                return false;
            }
        }

        public async Task<T?> PopFromListAsync<T>(string key)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var value = await _database!.ListLeftPopAsync(fullKey);
                
                if (value.HasValue)
                {
                    return JsonSerializer.Deserialize<T>(value.ToString());
                }
                
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pop from list for key: {Key}", key);
                return default;
            }
        }

        public async Task<bool> SetSetAsync<T>(string key, HashSet<T> values, TimeSpan? expiry = null)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                
                // Clear existing set
                await _database!.KeyDeleteAsync(fullKey);
                
                // Add all values
                var tasks = values.Select(value => 
                {
                    var jsonValue = JsonSerializer.Serialize(value);
                    return _database.SetAddAsync(fullKey, jsonValue);
                });
                
                await Task.WhenAll(tasks);
                
                // Set expiry if specified
                if (expiry.HasValue)
                {
                    await _database.KeyExpireAsync(fullKey, expiry.Value);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set set for key: {Key}", key);
                return false;
            }
        }

        public async Task<HashSet<T>> GetSetAsync<T>(string key)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var values = await _database!.SetMembersAsync(fullKey);
                
                var result = new HashSet<T>();
                foreach (var value in values)
                {
                    if (value.HasValue)
                    {
                        var item = JsonSerializer.Deserialize<T>(value.ToString());
                        if (item != null)
                            result.Add(item);
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get set for key: {Key}", key);
                return new HashSet<T>();
            }
        }

        public async Task<bool> AddToSetAsync<T>(string key, T value)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var jsonValue = JsonSerializer.Serialize(value);
                return await _database!.SetAddAsync(fullKey, jsonValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add to set for key: {Key}", key);
                return false;
            }
        }

        public async Task<bool> RemoveFromSetAsync<T>(string key, T value)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var jsonValue = JsonSerializer.Serialize(value);
                return await _database!.SetRemoveAsync(fullKey, jsonValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove from set for key: {Key}", key);
                return false;
            }
        }

        public async Task<bool> IsInSetAsync<T>(string key, T value)
        {
            try
            {
                await EnsureConnectionAsync();
                var fullKey = GetFullKey(key);
                var jsonValue = JsonSerializer.Serialize(value);
                return await _database!.SetContainsAsync(fullKey, jsonValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check set membership for key: {Key}", key);
                return false;
            }
        }

        private string GetFullKey(string key)
        {
            return $"{_options.KeyPrefix}{key}";
        }

        private async Task EnsureConnectionAsync()
        {
            if (_redis?.IsConnected != true || _database == null)
            {
                await ConnectAsync();
            }
        }

        private string Compress(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                msi.CopyTo(gs);
            }
            return Convert.ToBase64String(mso.ToArray());
        }

        private string Decompress(string input)
        {
            var bytes = Convert.FromBase64String(input);
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(msi, CompressionMode.Decompress))
            {
                gs.CopyTo(mso);
            }
            return Encoding.UTF8.GetString(mso.ToArray());
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            GC.SuppressFinalize(this);
        }
    }
}
