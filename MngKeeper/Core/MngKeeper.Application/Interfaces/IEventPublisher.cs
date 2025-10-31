namespace MngKeeper.Application.Interfaces
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(T @event, string domainId) where T : class;
        Task PublishAsync<T>(T @event, string domainId, string routingKey) where T : class;
    }

    public abstract class BaseEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string DomainId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? CorrelationId { get; set; }
    }

    // Domain Events
    public class DomainCreatedEvent : BaseEvent
    {
        public string DomainName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
    }

    public class UserCreatedEvent : BaseEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
    }

    public class UserUpdatedEvent : BaseEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
    }

    public class UserDeletedEvent : BaseEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class GroupCreatedEvent : BaseEvent
    {
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    public class GroupUpdatedEvent : BaseEvent
    {
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    public class GroupDeletedEvent : BaseEvent
    {
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }

    public class UserAddedToGroupEvent : BaseEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }

    public class UserRemovedFromGroupEvent : BaseEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }
}
