using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;

namespace MngAlarm.Infrastructure.Http;

public sealed class AlarmActorAccessor(IHttpContextAccessor httpContextAccessor) : IAlarmActorAccessor
{
    public AlarmActorContext GetCurrentActor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return new AlarmActorContext(null, null);

        var userId = FirstClaim(user, "sub", ClaimTypes.NameIdentifier);
        var userName = FirstClaim(
            user,
            "preferred_username",
            "username",
            ClaimTypes.Name,
            "name");

        if (string.IsNullOrWhiteSpace(userName))
            userName = userId;

        return new AlarmActorContext(
            string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(),
            string.IsNullOrWhiteSpace(userName) ? null : userName.Trim());
    }

    private static string? FirstClaim(ClaimsPrincipal user, params string[] types)
    {
        foreach (var type in types)
        {
            var value = user.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
