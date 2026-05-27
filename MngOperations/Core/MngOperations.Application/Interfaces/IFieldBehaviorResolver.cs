using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.FieldBehaviors;

namespace MngOperations.Application.Interfaces;

public interface IFieldBehaviorResolver
{
    Task<IReadOnlyDictionary<string, FieldBehaviorDto>> ResolveAllAsync(
        FieldBehaviorResolveContext context,
        CancellationToken cancellationToken = default);

    Task<FieldBehaviorDto> ResolveAsync(
        FieldBehaviorResolveContext context,
        string fieldName,
        CancellationToken cancellationToken = default);

    void EnsureWritableFields(
        FieldBehaviorResolveContext context,
        IReadOnlyDictionary<string, FieldBehaviorDto> behaviors,
        IEnumerable<string> fieldKeys);
}
