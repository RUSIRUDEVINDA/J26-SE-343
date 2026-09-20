namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public interface ILeaseCaseRepository
{
    Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default);

    Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default);

    Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default);
}
