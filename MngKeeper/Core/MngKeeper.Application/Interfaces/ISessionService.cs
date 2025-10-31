namespace MngKeeper.Application.Interfaces
{
    public interface ISessionService
    {
        Task<string> CreateSessionAsync(SessionData sessionData, TimeSpan? expiry = null);
        Task<SessionData?> GetSessionAsync(string sessionId);
        Task<bool> UpdateSessionAsync(string sessionId, SessionData sessionData);
        Task<bool> DeleteSessionAsync(string sessionId);
        Task<bool> ExtendSessionAsync(string sessionId, TimeSpan? expiry = null);
        Task<bool> IsSessionValidAsync(string sessionId);
        Task<List<string>> GetActiveSessionsForUserAsync(string userId);
        Task<bool> InvalidateAllSessionsForUserAsync(string userId);
        Task<long> GetActiveSessionCountAsync();
    }

    public class SessionOptions
    {
        public TimeSpan DefaultSessionExpiry { get; set; } = TimeSpan.FromHours(8);
        public TimeSpan SlidingExpiry { get; set; } = TimeSpan.FromMinutes(30);
        public string SessionKeyPrefix { get; set; } = "session:";
        public string UserSessionsKeyPrefix { get; set; } = "user_sessions:";
        public bool EnableSlidingExpiry { get; set; } = true;
    }
}
