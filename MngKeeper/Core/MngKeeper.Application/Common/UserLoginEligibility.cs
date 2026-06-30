using MngKeeper.Application.Directory;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Common;

public static class UserLoginEligibility
{
    public static bool CanAuthenticate(User user) =>
        ApplicationScopeDefaults.CanAuthenticate(user.IsActive, user.IncludeInApplication);
}
