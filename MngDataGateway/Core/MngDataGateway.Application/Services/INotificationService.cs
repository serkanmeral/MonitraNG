using System.Threading.Tasks;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// Service for publishing data events asynchronously
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Publish data created event (async - fire & forget)
        /// </summary>
        Task PublishDataCreatedEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            object data,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Publish data updated event (async - fire & forget)
        /// </summary>
        Task PublishDataUpdatedEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            object data,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Publish data deleted event (async - fire & forget)
        /// </summary>
        Task PublishDataDeletedEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Publish data restored event (async - fire & forget)
        /// </summary>
        Task PublishDataRestoredEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null);
    }
}

