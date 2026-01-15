namespace MngKeeper.Application.Interfaces;

/// <summary>
/// Service for MinIO object storage operations
/// </summary>
public interface IMinioService
{
    /// <summary>
    /// Creates a new bucket
    /// </summary>
    Task<bool> CreateBucketAsync(string bucketName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a bucket exists
    /// </summary>
    Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a bucket (must be empty)
    /// </summary>
    Task<bool> DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates folder structure in a bucket
    /// </summary>
    Task<bool> CreateFolderStructureAsync(string bucketName, string[] folders, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets bucket policy
    /// </summary>
    Task<bool> SetBucketPolicyAsync(string bucketName, string policy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an object from MinIO as a stream
    /// </summary>
    Task<Stream?> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uploads an object to MinIO from a stream
    /// </summary>
    Task<bool> PutObjectAsync(string bucketName, string objectName, Stream content, string contentType, CancellationToken cancellationToken = default);
    
    Task<bool> RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);
}

