using Microsoft.AspNetCore.Http;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;

namespace MngAlarm.Infrastructure.Http;

public sealed class AlarmDomainAccessor(IHttpContextAccessor httpContextAccessor) : IAlarmDomainAccessor
{
    public AlarmDomainContext GetRequiredDomain()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var domainName = user?.FindFirst("domain_name")?.Value
            ?? httpContextAccessor.HttpContext?.Request.Headers["X-Domain-Name"].FirstOrDefault()
            ?? httpContextAccessor.HttpContext?.Request.Query["domainName"].FirstOrDefault();

        var domainId = user?.FindFirst("domain_id")?.Value
            ?? httpContextAccessor.HttpContext?.Request.Headers["X-Domain-Id"].FirstOrDefault()
            ?? httpContextAccessor.HttpContext?.Request.Query["domainId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(domainName))
            throw new InvalidOperationException("domain_name claim, X-Domain-Name header, or domainName query is required.");

        if (string.IsNullOrWhiteSpace(domainId))
            domainId = domainName;

        return new AlarmDomainContext(domainId, domainName);
    }
}
