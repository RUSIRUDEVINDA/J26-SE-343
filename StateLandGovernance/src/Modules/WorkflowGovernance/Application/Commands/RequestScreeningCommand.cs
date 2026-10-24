namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;

/// <summary>
/// Command to request Component 4 screening for a lease case.
/// Internal/system orchestration command executed once verified facts are linked.
/// Requires the lease case to have an authoritative CurrentVerifiedFactSnapshot linked.
/// Records a Pending screening outcome bound to the current snapshot ID and commits once.
/// Asynchronous dispatch to Component 4 is handled via transactional outbox / background worker.
/// </summary>
public sealed record RequestScreeningCommand(
    Guid LeaseCaseId,
    int ExpectedLeaseCaseRevision,
    Guid? ScreeningRequestId = null,
    string? Remarks = null
) : ICommand;
