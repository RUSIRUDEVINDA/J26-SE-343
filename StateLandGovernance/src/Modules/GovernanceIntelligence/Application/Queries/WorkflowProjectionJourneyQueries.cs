using StateLandGovernance.GovernanceIntelligence.Application.Projections;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record WorkflowProjectionEventDto(
    int EventSequence,
    string Activity,
    string Institution,
    string Resource,
    string ActorRole,
    string Timestamp,
    string Status,
    string? EvidenceReference);

public sealed record AvailableValueDto<T>(string Availability, T? Value);

public sealed record WorkflowBoundaryDto(
    string Availability,
    DateTimeOffset? OccurredAt,
    string Source,
    string SourceReference);

public sealed record WorkflowRouteApplicabilityDto(
    AvailableValueDto<IReadOnlyList<string>> RequiredClearances,
    AvailableValueDto<bool?> HigherApprovalApplicable,
    AvailableValueDto<bool?> RegistrationApplicable);

public sealed record WorkflowProjectionJourneyDto(
    Guid ProjectionId,
    string CaseId,
    Guid WorkflowRunId,
    string WorkflowState,
    string TraceCompleteness,
    string AssessmentEligibility,
    AvailableValueDto<string> LegalRouteLabel,
    bool IsSynthetic,
    string ProvenanceKind,
    string Purpose,
    string? SyntheticSeedId,
    string? SyntheticSeedVersion,
    IReadOnlyList<WorkflowProjectionEventDto> Events,
    WorkflowRouteApplicabilityDto RouteApplicability,
    WorkflowBoundaryDto CompleteProposalBoundary,
    WorkflowBoundaryDto ApplicantHandoverBoundary,
    DateTimeOffset IngestedAtUtc);

public sealed record GetWorkflowProjectionJourneyByCaseQuery(string CaseId);

/// <summary>
/// Reads the newest persisted Component 3 projection for a case without mutating it or
/// inferring route facts that Component 4 has not stored.
/// </summary>
public sealed class GetWorkflowProjectionJourneyByCaseQueryHandler
{
    private const string Unavailable = "Unavailable";
    private readonly IComponent3WorkflowGovernanceProjectionStore _store;

    public GetWorkflowProjectionJourneyByCaseQueryHandler(IComponent3WorkflowGovernanceProjectionStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<WorkflowProjectionJourneyDto?> HandleAsync(
        GetWorkflowProjectionJourneyByCaseQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.CaseId))
        {
            throw new ArgumentException("CaseId cannot be blank.", nameof(query));
        }

        var projections = await _store
            .GetByCaseIdAsync(query.CaseId.Trim(), 1, cancellationToken)
            .ConfigureAwait(false);
        var projection = projections.SingleOrDefault();
        return projection is null ? null : Map(projection);
    }

    private static WorkflowProjectionJourneyDto Map(Component3WorkflowGovernanceProjection projection)
    {
        var eligibility = projection.TraceCompleteness ==
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring
                ? "Eligible"
                : "Ineligible";

        return new WorkflowProjectionJourneyDto(
            projection.ProjectionId,
            projection.CaseId,
            projection.WorkflowRunId,
            projection.TraceStatus.ToString(),
            projection.TraceCompleteness.ToString(),
            eligibility,
            new AvailableValueDto<string>(Unavailable, null),
            projection.Provenance.IsSynthetic,
            projection.Provenance.ProvenanceKind.ToString(),
            projection.Provenance.Purpose.ToString(),
            projection.Provenance.SyntheticSeedId,
            projection.Provenance.SyntheticSeedVersion,
            projection.Events.Select(item => new WorkflowProjectionEventDto(
                item.EventSeq,
                item.Activity,
                item.InstitutionCode,
                item.Resource,
                item.ActorRole,
                item.AnomalyInputTimestamp ?? item.Timestamp.ToString("O"),
                item.EventOutcome,
                item.EvidenceRef)).ToArray(),
            new WorkflowRouteApplicabilityDto(
                new AvailableValueDto<IReadOnlyList<string>>(Unavailable, null),
                new AvailableValueDto<bool?>(Unavailable, null),
                new AvailableValueDto<bool?>(Unavailable, null)),
            MapBoundary(projection.CompleteProposalRoadmapStart),
            MapBoundary(projection.ApprovalHandover),
            projection.IngestedAtUtc);
    }

    private static WorkflowBoundaryDto MapBoundary(Timing.TimingBoundaryEvidence boundary) =>
        new(
            boundary.Status.ToString(),
            boundary.OccurredAt,
            boundary.Source,
            boundary.SourceReference);
}
