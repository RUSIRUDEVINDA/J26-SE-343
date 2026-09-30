namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class RegisterLeaseCaseCommandHandler : ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly RegisterLeaseCaseCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public RegisterLeaseCaseCommandHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        RegisterLeaseCaseCommandValidator validator,
        TimeProvider timeProvider)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<LeaseCaseDto> HandleAsync(
        RegisterLeaseCaseCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        // 1. Validate command boundary inputs
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        // 2. Perform duplicate reference check (Application-level pre-check).
        // NOTE: This pre-check provides immediate user feedback in normal flows, but is NOT
        // sufficient to guarantee race-free uniqueness under high concurrency. Authoritative
        // uniqueness must be enforced at the database level via a unique index on ApplicationReference.
        // Persistence conflict exceptions from the database constraint will be translated at the infrastructure boundary.
        var trimmedReference = command.ApplicationReference.Trim();
        var existing = await _leaseCaseRepository.GetByApplicationReferenceAsync(trimmedReference, cancellationToken);
        if (existing != null)
        {
            throw new DuplicateApplicationReferenceException(trimmedReference);
        }

        // 3. Map boundary authority context and proposal intake to Domain value objects
        var authoritySnapshot = command.AuthorityContext.ToDomain();
        var proposalIntake = command.ProposalIntake?.ToDomain();

        // 4. Create the Domain aggregate using the frozen Domain API.
        // The Domain constructor strictly evaluates authority capability ("LeaseInitiator"),
        // validity period, and scope coverage against the resolved LeaseCaseId.
        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;
        var caseId = command.LeaseCaseId.HasValue && command.LeaseCaseId.Value != Guid.Empty
            ? new LeaseCaseId(command.LeaseCaseId.Value)
            : new LeaseCaseId(Guid.NewGuid());

        var leaseCase = new LeaseCase(
            caseId,
            trimmedReference,
            command.ActorId,
            actionTime,
            authoritySnapshot,
            proposalIntake);

        // 5. Add the aggregate to the repository
        await _leaseCaseRepository.AddAsync(leaseCase, cancellationToken);

        // 6. Commit once via unit of work
        await _unitOfWork.CommitAsync(cancellationToken);

        // 7. Map the resulting aggregate to the response DTO
        return LeaseCaseMapper.ToDto(leaseCase);
    }
}
