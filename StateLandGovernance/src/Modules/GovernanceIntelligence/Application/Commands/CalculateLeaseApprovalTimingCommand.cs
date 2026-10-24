using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

public sealed record CalculateLeaseApprovalTimingCommand(
    Guid WorkflowProjectionId,
    DateTimeOffset? AssessmentAtUtc = null);

/// <summary>
/// Calculates and idempotently persists the non-statutory two-calendar-month timing assessment
/// from an exact persisted Component 3 projection.
/// </summary>
public sealed class CalculateLeaseApprovalTimingCommandHandler
{
    private readonly IComponent3WorkflowGovernanceProjectionStore _projectionStore;
    private readonly ILeaseApprovalTimingCalculator _calculator;
    private readonly ILeaseApprovalTimingAssessmentStore _assessmentStore;
    private readonly TimeProvider _timeProvider;

    public CalculateLeaseApprovalTimingCommandHandler(
        IComponent3WorkflowGovernanceProjectionStore projectionStore,
        ILeaseApprovalTimingCalculator calculator,
        ILeaseApprovalTimingAssessmentStore assessmentStore,
        TimeProvider timeProvider)
    {
        _projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _assessmentStore = assessmentStore ?? throw new ArgumentNullException(nameof(assessmentStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<LeaseApprovalTimingAssessment> HandleAsync(
        CalculateLeaseApprovalTimingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.WorkflowProjectionId == Guid.Empty)
            throw new ArgumentException("WorkflowProjectionId cannot be empty.", nameof(command));

        var projection = await _projectionStore.GetByIdAsync(command.WorkflowProjectionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Workflow projection '{command.WorkflowProjectionId}' was not found.");

        var workflowState = projection.TraceStatus == Component3WorkflowTraceStatus.Ongoing
            ? LeaseApprovalWorkflowState.Ongoing
            : LeaseApprovalWorkflowState.Completed;
        var assessmentAt = command.AssessmentAtUtc ?? _timeProvider.GetUtcNow();
        var input = new LeaseApprovalTimingAssessmentInput(
            projection.CaseId,
            projection.WorkflowRunId,
            workflowState,
            projection.CompleteProposalRoadmapStart,
            projection.ApprovalHandover,
            assessmentAt);

        var inputSha256 = LeaseApprovalTimingAssessment.ComputeCanonicalInputSha256(input, projection.Provenance);
        var existing = await _assessmentStore.GetByWorkflowRunPolicyAndHashAsync(
            projection.WorkflowRunId,
            LeaseApprovalTimingCalculator.PolicyVersion,
            inputSha256,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null) return existing;

        var calculated = _calculator.Assess(input);
        var assessment = LeaseApprovalTimingAssessment.FromCalculationResult(
            Guid.NewGuid(),
            calculated,
            projection.ProjectionId,
            inputSha256,
            projection.Provenance,
            _timeProvider.GetUtcNow());

        return await _assessmentStore.UpsertAsync(assessment, cancellationToken).ConfigureAwait(false);
    }
}
