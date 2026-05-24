using MngKeeper.Application.Common.Constants;

namespace MngKeeper.Application.Directory;

/// <summary>
/// Varsayılan MonitraNG grupları her zaman Local kalır; diğer KC realm grupları sync ile Directory olur.
/// </summary>
public static class DirectoryGroupPolicy
{
  public static bool IsProtectedLocalGroupName(string? name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return false;

    return SystemGroups.All.Any(
      g => string.Equals(g, name.Trim(), StringComparison.OrdinalIgnoreCase));
  }
}
