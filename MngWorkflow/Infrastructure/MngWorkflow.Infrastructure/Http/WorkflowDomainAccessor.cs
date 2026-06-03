using Microsoft.AspNetCore.Http;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Infrastructure.Http;

public sealed class WorkflowDomainAccessor(IHttpContextAccessor httpContextAccessor) : IWorkflowDomainAccessor
{
    public WorkflowDomainContext GetRequiredDomain()
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

        return new WorkflowDomainContext(domainId, domainName);
    }
}
