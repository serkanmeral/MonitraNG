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
}

