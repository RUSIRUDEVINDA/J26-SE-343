using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

public sealed record ApplyPp1SeedScenarioCommand(
    string CaseId,
    Guid Component1ProjectionId,
    Guid Component3ProjectionId,
    Guid WorkflowRunId,
    DateTimeOffset GeneratedAtUtc,
    string SyntheticSeedVersion = GovernanceInputProvenance.FinalPp1SyntheticSeedVersion);

public sealed record ApplyPp1SeedScenarioResult(
    string CaseId,
    Guid Component1ProjectionId,
    Guid Component3ProjectionId,
    Guid WorkflowRunId,
    string ProvenanceKind,
    string Purpose,
    string SyntheticSeedVersion);

/// <summary>
/// Development-only ingestion handler for the five reconciled PP1 final scenarios.
/// It writes only Component 4-owned projections and is idempotent through their stores.
/// </summary>
public sealed class ApplyPp1SeedScenarioCommandHandler
{
    private const string CommissionerCaseId = "PP1-ANOMALY-COMMISSIONER-001";
    private static readonly HashSet<string> ApprovedCases = new(StringComparer.Ordinal)
    {
        "PP1-FINAL-CLEAR-001",
        "PP1-FINAL-REFERRAL-001",
        "PP1-FINAL-DELAYED-001",
        "PP1-FINAL-COMPLAINT-001",
        "PP1-FINAL-COMBINED-001",
        CommissionerCaseId
    };

    private readonly IComponent1GovernanceCaseProjectionStore _component1Store;
    private readonly IComponent3WorkflowGovernanceProjectionStore _component3Store;

    public ApplyPp1SeedScenarioCommandHandler(
        IComponent1GovernanceCaseProjectionStore component1Store,
        IComponent3WorkflowGovernanceProjectionStore component3Store)
    {
        _component1Store = component1Store ?? throw new ArgumentNullException(nameof(component1Store));
        _component3Store = component3Store ?? throw new ArgumentNullException(nameof(component3Store));
    }

    public async Task<ApplyPp1SeedScenarioResult> HandleAsync(
        ApplyPp1SeedScenarioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!ApprovedCases.Contains(command.CaseId))
        {
            throw new ArgumentException(
                "CaseId is not one of the five approved PP1 final scenarios; original PP1 IDs cannot be reused.",
                nameof(command));
        }

        var expectedVersion = command.CaseId == CommissionerCaseId
            ? GovernanceInputProvenance.CommissionerAnomalySyntheticSeedVersion
            : GovernanceInputProvenance.FinalPp1SyntheticSeedVersion;
        if (!string.Equals(command.SyntheticSeedVersion, expectedVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"SyntheticSeedVersion does not match the approved contract for CaseId '{command.CaseId}'.",
                nameof(command));
        }

        if (command.Component1ProjectionId == Guid.Empty ||
            command.Component3ProjectionId == Guid.Empty ||
            command.WorkflowRunId == Guid.Empty)
        {
            throw new ArgumentException("Projection and workflow identifiers cannot be empty.", nameof(command));
        }

        if (command.GeneratedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("GeneratedAtUtc must have a zero UTC offset.", nameof(command));
        }

        var version = expectedVersion;
        var c1Provenance = GovernanceInputProvenance.CreatePp1Fixture(
            "SyntheticPp1Seed.Component1",
            $"{command.CaseId}:C1",
            $"{version}:component1",
            command.GeneratedAtUtc,
            command.CaseId,
            command.GeneratedAtUtc,
            version);
        var c3Provenance = GovernanceInputProvenance.CreatePp1Fixture(
            "SyntheticPp1Seed.Component3",
            $"{command.CaseId}:C3",
            $"{version}:component3",
            command.GeneratedAtUtc,
            command.CaseId,
            command.GeneratedAtUtc,
            version);

        var reviewScenario = command.CaseId is "PP1-FINAL-REFERRAL-001" or "PP1-FINAL-COMBINED-001";
        var zones = reviewScenario ? new[] { "PP1-SENSITIVE-ZONE" } : Array.Empty<string>();
        var c1Hash = Component1GovernanceCaseProjection.ComputeCanonicalSha256(
            command.CaseId,
            $"PARCEL-{command.CaseId}",
            "PP1-DIV",
            "Hambantota",
            "Southern",
            "PP1-GN",
            "PublicPurposeLease",
            1.2500m,
            Component1OwnershipEvidenceStatus.EvidenceProvided,
            reviewScenario
                ? Component1ProtectedZoneOverlapState.ProtectedZoneOverlapDetected
                : Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
            zones,
            reviewScenario
                ? Component1BoundaryConfirmationStatus.BoundaryDisputed
                : Component1BoundaryConfirmationStatus.BoundaryConfirmed,
            reviewScenario,
            new[] { $"SyntheticPp1Seed:{command.CaseId}:parcel-evidence" },
            c1Provenance);
        var c1 = new Component1GovernanceCaseProjection(
            command.Component1ProjectionId,
            command.CaseId,
            $"PARCEL-{command.CaseId}",
            "PP1-DIV",
            "Hambantota",
            "Southern",
            "PP1-GN",
            "PublicPurposeLease",
            1.2500m,
            Component1OwnershipEvidenceStatus.EvidenceProvided,
            reviewScenario
                ? Component1ProtectedZoneOverlapState.ProtectedZoneOverlapDetected
                : Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
            zones,
            reviewScenario
                ? Component1BoundaryConfirmationStatus.BoundaryDisputed
                : Component1BoundaryConfirmationStatus.BoundaryConfirmed,
            reviewScenario,
            new[] { $"SyntheticPp1Seed:{command.CaseId}:parcel-evidence" },
            c1Hash,
            c1Provenance,
            command.GeneratedAtUtc);

        var c3 = CreateWorkflowProjection(command, c3Provenance);
        var savedC1 = await _component1Store.UpsertAsync(c1, cancellationToken).ConfigureAwait(false);
        var savedC3 = await _component3Store.UpsertAsync(c3, cancellationToken).ConfigureAwait(false);

        return new ApplyPp1SeedScenarioResult(
            command.CaseId,
            savedC1.ProjectionId,
            savedC3.ProjectionId,
            savedC3.WorkflowRunId,
            savedC3.Provenance.ProvenanceKind.ToString(),
            savedC3.Provenance.Purpose.ToString(),
            savedC3.Provenance.SyntheticSeedVersion!);
    }

    private static Component3WorkflowGovernanceProjection CreateWorkflowProjection(
        ApplyPp1SeedScenarioCommand command,
        GovernanceInputProvenance provenance)
    {
        var ongoing = command.CaseId is "PP1-FINAL-REFERRAL-001" or "PP1-FINAL-COMPLAINT-001";
        var delayed = command.CaseId is "PP1-FINAL-DELAYED-001" or "PP1-FINAL-COMBINED-001";
        if (command.CaseId == CommissionerCaseId)
        {
            return CreateCommissionerWorkflowProjection(command, provenance);
        }

        var activities = command.CaseId switch
        {
            "PP1-FINAL-REFERRAL-001" => new[]
            {
                "Application Received", "Document Review", "Field Inspection", "Regional Approval"
            },
            "PP1-FINAL-COMPLAINT-001" => new[]
            {
                "Application Received", "Document Review", "Field Inspection"
            },
            "PP1-FINAL-COMBINED-001" => new[]
            {
                "Application Received", "Document Review", "Correction Requested", "Document Review",
                "Field Inspection", "Regional Approval", Component3WorkflowGovernanceProjection.AnomalyModelV1TerminalActivity
            },
            _ => new[]
            {
                "Application Received", "Document Review", "Field Inspection", "Regional Approval",
                Component3WorkflowGovernanceProjection.AnomalyModelV1TerminalActivity
            }
        };
        var start = command.GeneratedAtUtc.AddDays(-100);
        var gapDays = delayed ? 12 : 7;
        var events = activities.Select((activity, index) =>
        {
            var instant = start.AddDays(index * gapDays);
            return new Component3WorkflowProjectionEvent(
                $"{command.CaseId}-EVT-{index + 1:D2}",
                index + 1,
                activity,
                $"STAGE-{index + 1:D2}",
                index % 2 == 0 ? "LandOfficer" : "ReviewOfficer",
                index % 3 == 0 ? "DS" : "LCGD",
                instant,
                "Completed",
                $"SyntheticPp1Seed:{command.CaseId}:event:{index + 1}",
                index % 2 == 0 ? "Officer-A" : "Officer-B",
                instant.ToString("yyyy-MM-dd'T'HH:mm:ss"));
        }).ToList();

        var roadmapStart = command.CaseId == "PP1-FINAL-COMPLAINT-001"
            ? new TimingBoundaryEvidence(
                null,
                TimingBoundaryEvidenceStatus.Unconfirmed,
                "SyntheticPp1Seed",
                $"{command.CaseId}:roadmap-start-unconfirmed",
                command.CaseId,
                command.WorkflowRunId)
            : new TimingBoundaryEvidence(
                start,
                TimingBoundaryEvidenceStatus.Confirmed,
                "SyntheticPp1Seed",
                $"{command.CaseId}:roadmap-start",
                command.CaseId,
                command.WorkflowRunId);
        var handover = ongoing
            ? new TimingBoundaryEvidence(
                null,
                TimingBoundaryEvidenceStatus.Unavailable,
                "SyntheticPp1Seed",
                $"{command.CaseId}:approval-handover-unavailable",
                command.CaseId,
                command.WorkflowRunId)
            : new TimingBoundaryEvidence(
                start.AddDays(delayed ? 75 : 45),
                TimingBoundaryEvidenceStatus.Confirmed,
                "SyntheticPp1Seed",
                $"{command.CaseId}:approval-handover",
                command.CaseId,
                command.WorkflowRunId);
        var traceStatus = ongoing
            ? Component3WorkflowTraceStatus.Ongoing
            : Component3WorkflowTraceStatus.Completed;
        var completeness = ongoing
            ? Component3WorkflowTraceCompleteness.PartialTraceIneligibleForAnomalyScoring
            : Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring;
        var stageCode = command.CaseId switch
        {
            "PP1-FINAL-REFERRAL-001" => "REGIONAL_APPROVAL",
            "PP1-FINAL-COMPLAINT-001" => "FIELD_INSPECTION",
            _ => "DS_FINAL_NOTIFICATION"
        };
        var inputHash = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            command.WorkflowRunId,
            command.CaseId,
            "LandLeaseApproval",
            traceStatus,
            completeness,
            stageCode,
            Component3EventTimestampSemantics.ExactUtcTimestamp,
            Component3WorkflowGovernanceProjection.RequiredColomboTimeZone,
            events,
            roadmapStart,
            handover,
            provenance);

        return new Component3WorkflowGovernanceProjection(
            command.Component3ProjectionId,
            command.WorkflowRunId,
            command.CaseId,
            "LandLeaseApproval",
            traceStatus,
            completeness,
            stageCode,
            Component3EventTimestampSemantics.ExactUtcTimestamp,
            Component3WorkflowGovernanceProjection.RequiredColomboTimeZone,
            events,
            roadmapStart,
            handover,
            inputHash,
            provenance,
            command.GeneratedAtUtc);
    }

    private static Component3WorkflowGovernanceProjection CreateCommissionerWorkflowProjection(
        ApplyPp1SeedScenarioCommand command,
        GovernanceInputProvenance provenance)
    {
        var start = command.GeneratedAtUtc.AddDays(-70);
        var definitions = new (string Activity, string Stage, string Role, string Institution, string Resource, int Day, string Outcome)[]
        {
            ("Complete Proposal Received", "DS_COMPLETE_PROPOSAL", "IntakeOfficer", "DS", "DS-Intake-01", 0, "Completed"),
            ("DS Scrutiny", "DS_SCRUTINY", "LandOfficer", "DS", "DS-Land-02", 3, "Completed"),
            ("DS Field Verification", "DS_FIELD_VERIFICATION", "FieldOfficer", "DS", "DS-Field-03", 7, "Completed"),
            ("Environmental Clearance", "TECHNICAL_CLEARANCE_ENV", "EnvironmentalOfficer", "CEA", "CEA-Review-01", 10, "Completed"),
            ("Survey Clearance", "TECHNICAL_CLEARANCE_SURVEY", "SurveyOfficer", "SURVEY_DEPARTMENT", "Survey-Review-04", 13, "Completed"),
            ("PLC/DLC Review and Recommendation", "PLC_DLC_RECOMMENDATION", "CommitteeSecretary", "PLC_DLC", "PLC-DLC-Committee", 17, "Completed"),
            ("LCGD Intake", "LCGD_INTAKE", "IntakeOfficer", "LCGD", "LCGD-Intake-01", 20, "Completed"),
            ("LCGD Technical Review", "LCGD_TECHNICAL_REVIEW", "ReviewOfficer", "LCGD", "LCGD-Review-07", 24, "Completed"),
            ("Correction Requested", "LCGD_CORRECTION_REQUEST", "ReviewOfficer", "LCGD", "LCGD-Review-07", 27, "Completed"),
            ("LCGD Technical Review (Re-submission)", "LCGD_TECHNICAL_REVIEW", "ReviewOfficer", "LCGD", "LCGD-Review-07", 45, "Completed"),
            ("Ministry/Competent Authority Approval", "HIGHER_APPROVAL", "ApprovingOfficer", "MINISTRY_OF_LANDS", "Ministry-Approval-02", 49, "Completed"),
            ("Presidential Secretariat Processing", "PRESIDENTIAL_PROCESSING", "RouteController", "PRESIDENTIAL_SECRETARIAT", "Route-Control", 49, "NotApplicable"),
            ("Lease Instrument Finalization", "LEASE_INSTRUMENT_FINALIZATION", "LegalOfficer", "LCGD", "LCGD-Legal-03", 54, "Completed"),
            ("Land Registry Registration", "LAND_REGISTRY_REGISTRATION", "RouteController", "LAND_REGISTRY", "Route-Control", 54, "NotApplicable"),
            ("Return to DS", "RETURN_TO_DS", "DispatchOfficer", "DS", "DS-Dispatch-01", 58, "Completed"),
            (Component3WorkflowGovernanceProjection.AnomalyModelV1TerminalActivity, "DS_FINAL_NOTIFICATION", "LandOfficer", "DS", "DS-Land-02", 60, "Completed")
        };

        var events = definitions.Select((definition, index) =>
        {
            var instant = start.AddDays(definition.Day);
            return new Component3WorkflowProjectionEvent(
                $"{command.CaseId}-EVT-{index + 1:D2}",
                index + 1,
                definition.Activity,
                definition.Stage,
                definition.Role,
                definition.Institution,
                instant,
                definition.Outcome,
                $"SyntheticPp1Seed:{command.CaseId}:event:{index + 1}",
                definition.Resource,
                instant.ToString("yyyy-MM-dd'T'HH:mm:ss"));
        }).ToArray();

        var roadmapStart = new TimingBoundaryEvidence(
            start,
            TimingBoundaryEvidenceStatus.Confirmed,
            "SyntheticPp1Seed",
            $"{command.CaseId}:complete-proposal",
            command.CaseId,
            command.WorkflowRunId);
        var handover = new TimingBoundaryEvidence(
            start.AddDays(60),
            TimingBoundaryEvidenceStatus.Confirmed,
            "SyntheticPp1Seed",
            $"{command.CaseId}:applicant-final-notification",
            command.CaseId,
            command.WorkflowRunId);
        var inputHash = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            command.WorkflowRunId,
            command.CaseId,
            "LandLeaseApproval",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "DS_FINAL_NOTIFICATION",
            Component3EventTimestampSemantics.ExactUtcTimestamp,
            Component3WorkflowGovernanceProjection.RequiredColomboTimeZone,
            events,
            roadmapStart,
            handover,
            provenance);

        return new Component3WorkflowGovernanceProjection(
            command.Component3ProjectionId,
            command.WorkflowRunId,
            command.CaseId,
            "LandLeaseApproval",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "DS_FINAL_NOTIFICATION",
            Component3EventTimestampSemantics.ExactUtcTimestamp,
            Component3WorkflowGovernanceProjection.RequiredColomboTimeZone,
            events,
            roadmapStart,
            handover,
            inputHash,
            provenance,
            command.GeneratedAtUtc);
    }
}
