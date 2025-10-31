namespace MngKeeper.Application.Interfaces
{
    public interface IRedisService
    {
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<T?> GetAsync<T>(string key);
        Task<bool> DeleteAsync(string key);
        Task<bool> ExistsAsync(string key);
        Task<bool> SetExpiryAsync(string key, TimeSpan expiry);
        Task<TimeSpan?> GetTimeToLiveAsync(string key);
        Task<long> IncrementAsync(string key, long value = 1);
        Task<long> DecrementAsync(string key, long value = 1);
        Task<bool> SetHashAsync(string key, string field, string value);
        Task<string?> GetHashAsync(string key, string field);
        Task<Dictionary<string, string>> GetHashAllAsync(string key);
        Task<bool> DeleteHashAsync(string key, string field);
        Task<bool> SetListAsync<T>(string key, List<T> values, TimeSpan? expiry = null);
        Task<List<T>> GetListAsync<T>(string key);
        Task<bool> PushToListAsync<T>(string key, T value);
        Task<T?> PopFromListAsync<T>(string key);
        Task<bool> SetSetAsync<T>(string key, HashSet<T> values, TimeSpan? expiry = null);
        Task<HashSet<T>> GetSetAsync<T>(string key);
        Task<bool> AddToSetAsync<T>(string key, T value);
        Task<bool> RemoveFromSetAsync<T>(string key, T value);
        Task<bool> IsInSetAsync<T>(string key, T value);
        Task<bool> IsConnectedAsync();
        Task ConnectAsync();
        Task DisconnectAsync();
    }

    public class CacheOptions
    {
        public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(30);
        public string KeyPrefix { get; set; } = "mngkeeper:";
        public bool EnableCompression { get; set; } = false;
    }

    public class SessionData
    {
        public string UserId { get; set; } = string.Empty;
        public string DomainId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public Dictionary<string, object> Claims { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
    }
}
