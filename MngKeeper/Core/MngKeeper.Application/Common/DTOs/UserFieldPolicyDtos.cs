namespace MngKeeper.Application.Common.DTOs;

public class UserCapabilitiesDto
{
  public bool CanChangePassword { get; set; }
  public bool CanManageGroups { get; set; }
  public bool CanDeactivate { get; set; }
  public bool CanDelete { get; set; }
}

public class UserFieldPolicyItemDto
{
  public bool Editable { get; set; }
  /// <summary>"directory" | "app" | "system"</summary>
  public string Source { get; set; } = "app";
}
