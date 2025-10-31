using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces
{
    public interface IAuditLogRepository : IRepository<AuditLog>
    {
        Task<IEnumerable<AuditLog>> GetByDomainIdAsync(string domainId);
        Task<IEnumerable<AuditLog>> GetByUserIdAsync(string userId);
        Task<IEnumerable<AuditLog>> GetByActionAsync(string action);
        Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<AuditLog>> GetByDomainIdAndDateRangeAsync(string domainId, DateTime startDate, DateTime endDate);
    }
}
