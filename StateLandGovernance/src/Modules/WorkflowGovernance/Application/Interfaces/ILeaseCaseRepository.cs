namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public interface ILeaseCaseRepository
{
    Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default);

    Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default);

    Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default);

    Task<(System.Collections.Generic.IReadOnlyList<LeaseCase> Items, int TotalCount)> ListAsync(
        int skip,
        int take,
        System.Guid? ownerActorId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<(System.Collections.Generic.IReadOnlyList<LeaseCase> Items, int TotalCount)>(
            (System.Array.Empty<LeaseCase>(), 0));
}
