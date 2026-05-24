using MngKeeper.Application.Common.DTOs;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces;

public interface IUserFieldPolicyService
{
  UserCapabilitiesDto GetCapabilities(User user);
  IReadOnlyDictionary<string, UserFieldPolicyItemDto> GetFieldPolicies(User user);
}
