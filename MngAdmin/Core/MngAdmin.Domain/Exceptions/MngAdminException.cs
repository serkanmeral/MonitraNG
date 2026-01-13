namespace MngAdmin.Domain.Exceptions;

/// <summary>
/// Base exception for MngAdmin domain
/// </summary>
public class MngAdminException : Exception
{
    public MngAdminException(string message) : base(message)
    {
    }

    public MngAdminException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
