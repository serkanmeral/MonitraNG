namespace MngKeeper.Domain.Exceptions
{
    public class DomainNotFoundException : Exception
    {
        public DomainNotFoundException(string message) : base(message) { }
        public DomainNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class DomainValidationException : Exception
    {
        public DomainValidationException(string message) : base(message) { }
        public DomainValidationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class DomainConflictException : Exception
    {
        public DomainConflictException(string message) : base(message) { }
        public DomainConflictException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class UserNotFoundException : Exception
    {
        public UserNotFoundException(string message) : base(message) { }
        public UserNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class GroupNotFoundException : Exception
    {
        public GroupNotFoundException(string message) : base(message) { }
        public GroupNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class UnauthorizedAccessException : Exception
    {
        public UnauthorizedAccessException(string message) : base(message) { }
        public UnauthorizedAccessException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ServiceUnavailableException : Exception
    {
        public ServiceUnavailableException(string message) : base(message) { }
        public ServiceUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }
}
