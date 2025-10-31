using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Infrastructure.Services
{
    public class SessionService : ISessionService
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<SessionService> _logger;
        private readonly SessionOptions _options;

        public SessionService(IRedisService redisService, ILogger<SessionService> logger)
        {
            _redisService = redisService;
            _logger = logger;
            _options = new SessionOptions();
        }

        public async Task<string> CreateSessionAsync(SessionData sessionData, TimeSpan? expiry = null)
        {
            try
            {
                var sessionId = Guid.NewGuid().ToString();
                var sessionKey = $"{_options.SessionKeyPrefix}{sessionId}";
                var userSessionsKey = $"{_options.UserSessionsKeyPrefix}{sessionData.UserId}";

                // Set session expiry
                var sessionExpiry = expiry ?? _options.DefaultSessionExpiry;

                // Store session data
                var success = await _redisService.SetAsync(sessionKey, sessionData, sessionExpiry);
                if (!success)
                {
                    _logger.LogError("Failed to store session data for session: {SessionId}", sessionId);
                    return string.Empty;
                }

                // Add session to user's active sessions
                await _redisService.AddToSetAsync(userSessionsKey, sessionId);
                await _redisService.SetExpiryAsync(userSessionsKey, sessionExpiry);

                _logger.LogInformation("Session created successfully: {SessionId} for user: {UserId}", sessionId, sessionData.UserId);
                return sessionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create session for user: {UserId}", sessionData.UserId);
                return string.Empty;
            }
        }

        public async Task<SessionData?> GetSessionAsync(string sessionId)
        {
            try
            {
                var sessionKey = $"{_options.SessionKeyPrefix}{sessionId}";
                var sessionData = await _redisService.GetAsync<SessionData>(sessionKey);

                if (sessionData != null && _options.EnableSlidingExpiry)
                {
                    // Extend session with sliding expiry
                    await ExtendSessionAsync(sessionId, _options.SlidingExpiry);
                }

                return sessionData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get session: {SessionId}", sessionId);
                return null;
            }
        }

        public async Task<bool> UpdateSessionAsync(string sessionId, SessionData sessionData)
        {
            try
            {
                var sessionKey = $"{_options.SessionKeyPrefix}{sessionId}";
                var ttl = await _redisService.GetTimeToLiveAsync(sessionKey);

                if (!ttl.HasValue)
                {
                    _logger.LogWarning("Session not found for update: {SessionId}", sessionId);
                    return false;
                }

                // Update session data with remaining TTL
                var success = await _redisService.SetAsync(sessionKey, sessionData, ttl.Value);
                
                if (success)
                {
                    _logger.LogInformation("Session updated successfully: {SessionId}", sessionId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session: {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            try
            {
                var sessionKey = $"{_options.SessionKeyPrefix}{sessionId}";
                
                // Get session data to find user ID
                var sessionData = await _redisService.GetAsync<SessionData>(sessionKey);
                if (sessionData != null)
                {
                    var userSessionsKey = $"{_options.UserSessionsKeyPrefix}{sessionData.UserId}";
                    await _redisService.RemoveFromSetAsync(userSessionsKey, sessionId);
                }

                var success = await _redisService.DeleteAsync(sessionKey);
                
                if (success)
                {
                    _logger.LogInformation("Session deleted successfully: {SessionId}", sessionId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete session: {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<bool> ExtendSessionAsync(string sessionId, TimeSpan? expiry = null)
        {
            try
            {
                var sessionKey = $"{_options.SessionKeyPrefix}{sessionId}";
                var sessionData = await _redisService.GetAsync<SessionData>(sessionKey);

                if (sessionData == null)
                {
                    _logger.LogWarning("Session not found for extension: {SessionId}", sessionId);
                    return false;
                }

                var extensionTime = expiry ?? _options.SlidingExpiry;
                var success = await _redisService.SetExpiryAsync(sessionKey, extensionTime);

                if (success)
                {
                    _logger.LogInformation("Session extended successfully: {SessionId} by {ExtensionTime}", sessionId, extensionTime);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extend session: {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<bool> IsSessionValidAsync(string sessionId)
        {
            try
            {
                var sessionKey = $"{_options.SessionKeyPrefix}{sessionId}";
                return await _redisService.ExistsAsync(sessionKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check session validity: {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<List<string>> GetActiveSessionsForUserAsync(string userId)
        {
            try
            {
                var userSessionsKey = $"{_options.UserSessionsKeyPrefix}{userId}";
                var sessionIds = await _redisService.GetSetAsync<string>(userSessionsKey);

                // Filter out expired sessions
                var activeSessions = new List<string>();
                foreach (var sessionId in sessionIds)
                {
                    if (await IsSessionValidAsync(sessionId))
                    {
                        activeSessions.Add(sessionId);
                    }
                    else
                    {
                        // Remove expired session from user's session list
                        await _redisService.RemoveFromSetAsync(userSessionsKey, sessionId);
                    }
                }

                return activeSessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active sessions for user: {UserId}", userId);
                return new List<string>();
            }
        }

        public async Task<bool> InvalidateAllSessionsForUserAsync(string userId)
        {
            try
            {
                var userSessionsKey = $"{_options.UserSessionsKeyPrefix}{userId}";
                var sessionIds = await _redisService.GetSetAsync<string>(userSessionsKey);

                var tasks = sessionIds.Select(sessionId => DeleteSessionAsync(sessionId));
                await Task.WhenAll(tasks);

                // Delete user sessions set
                await _redisService.DeleteAsync(userSessionsKey);

                _logger.LogInformation("All sessions invalidated for user: {UserId}, count: {SessionCount}", userId, sessionIds.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate all sessions for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<long> GetActiveSessionCountAsync()
        {
            try
            {
                // This is a simplified implementation
                // In a production environment, you might want to use Redis SCAN or maintain a separate counter
                var pattern = $"{_options.SessionKeyPrefix}*";
                
                // For now, we'll return 0 as getting exact count requires additional Redis operations
                // In a real implementation, you might maintain a separate counter or use Redis SCAN
                _logger.LogInformation("Getting active session count (simplified implementation)");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active session count");
                return 0;
            }
        }
    }
}
