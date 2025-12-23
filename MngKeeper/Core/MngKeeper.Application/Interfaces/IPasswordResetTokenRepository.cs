using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces
{
    public interface IPasswordResetTokenRepository
    {
        Task<PasswordResetToken?> GetByTokenAsync(string token);
        Task<PasswordResetToken> AddAsync(PasswordResetToken entity);
        Task<PasswordResetToken> UpdateAsync(PasswordResetToken entity);
        Task<bool> DeleteAsync(string id);
        Task<bool> ExistsAsync(string id);
        Task<bool> IsTokenValidAsync(string token);
        Task<bool> MarkTokenAsUsedAsync(string token);
        Task DeleteExpiredTokensAsync();
    }
}

