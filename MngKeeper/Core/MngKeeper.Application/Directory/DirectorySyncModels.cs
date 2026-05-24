namespace MngKeeper.Application.Directory;

public enum DirectorySyncTrigger
{
    Manual = 0,
    Scheduled = 1,
    Login = 2
}

public class DirectorySyncRequest
{
    public string? DomainId { get; set; }
    public DirectorySyncTrigger TriggeredBy { get; set; } = DirectorySyncTrigger.Manual;
}

public class DirectorySyncResult
{
    public bool IsSuccess { get; set; }
    public string Code { get; set; } = "success";
    public string Message { get; set; } = string.Empty;
    public string TriggeredBy { get; set; } = string.Empty;
    public string DomainId { get; set; } = string.Empty;
    public string RealmName { get; set; } = string.Empty;
    public int GroupsCreated { get; set; }
    public int GroupsUpdated { get; set; }
    public int UsersCreated { get; set; }
    public int UsersUpdated { get; set; }
    public int UsersSkipped { get; set; }
    public int UsersDeactivated { get; set; }
    public long DurationMs { get; set; }
    public List<string> Errors { get; set; } = new();
}
