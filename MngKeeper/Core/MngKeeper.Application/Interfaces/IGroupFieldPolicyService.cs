using MngKeeper.Application.Common.DTOs;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces;

public interface IGroupFieldPolicyService
{
  GroupCapabilitiesDto GetCapabilities(Group group);
}
