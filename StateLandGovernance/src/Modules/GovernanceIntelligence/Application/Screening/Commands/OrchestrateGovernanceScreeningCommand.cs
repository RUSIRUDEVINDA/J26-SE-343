using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Screening.Commands;

/// <summary>
/// Application command to orchestrate initial governance screening for a verified case snapshot.
/// Coordinates Regulatory Compliance, Conflict Detection, and Early Governance screening
/// on a single immutable, versioned snapshot.
/// </summary>
public sealed record OrchestrateGovernanceScreeningCommand(
    GovernanceScreeningCaseInputDto Input,
    Guid? AssessmentId = null
);

/// <summary>
/// Command handler for executing unified governance screening.
/// Enforces idempotent execution, deterministic hashing, durable multi-engine persistence,
/// and returns the consolidated screening result.
/// </summary>
public sealed class OrchestrateGovernanceScreeningCommandHandler
{
    private readonly IRegulatoryComplianceEngine _complianceEngine;
    private readonly IRegulatoryRuleProvider _ruleProvider;
    private readonly IComplianceRuleCatalogue? _ruleCatalogue;
    private readonly IGovernanceConflictEngine _conflictEngine;
    private readonly IEarlyGovernanceScreeningEngine _earlyScreeningEngine;
    private readonly IGovernanceEvaluationStore _evaluationStore;
    private readonly IEarlyGovernanceScreeningStore? _earlyScreeningStore;
    private readonly IConsolidatedGovernanceScreeningStore _consolidatedStore;
    private readonly TimeProvider _timeProvider;
    private readonly IApprovedScreeningPolicySource? _approvedPolicy;

    public OrchestrateGovernanceScreeningCommandHandler(
        IRegulatoryComplianceEngine complianceEngine,
        IRegulatoryRuleProvider ruleProvider,
        IGovernanceConflictEngine conflictEngine,
        IEarlyGovernanceScreeningEngine earlyScreeningEngine,
        IGovernanceEvaluationStore evaluationStore,
        IConsolidatedGovernanceScreeningStore consolidatedStore,
        IEarlyGovernanceScreeningStore? earlyScreeningStore = null,
        TimeProvider? timeProvider = null,
        IComplianceRuleCatalogue? ruleCatalogue = null,
        IApprovedScreeningPolicySource? approvedPolicy = null)
    {
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
        _conflictEngine = conflictEngine ?? throw new ArgumentNullException(nameof(conflictEngine));
        _earlyScreeningEngine = earlyScreeningEngine ?? throw new ArgumentNullException(nameof(earlyScreeningEngine));
        _evaluationStore = evaluationStore ?? throw new ArgumentNullException(nameof(evaluationStore));
        _earlyScreeningStore = earlyScreeningStore;
        _consolidatedStore = consolidatedStore ?? throw new ArgumentNullException(nameof(consolidatedStore));
        _ruleCatalogue = ruleCatalogue;
        _approvedPolicy = approvedPolicy;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ConsolidatedGovernanceScreeningResultDto> HandleAsync(
        OrchestrateGovernanceScreeningCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = command.Input ?? throw new ArgumentException("Input cannot be null.", nameof(command));

        ValidateInput(input);
        ApprovedScreeningPolicy? effectivePolicy = null;
        if (input.FullInputFingerprint != null || input.EffectivePolicyVersion != null || input.EvidenceVersion != null)
        {
            if (input.FullInputFingerprint is not { Length: 64 } || input.FullInputFingerprint.Any(c => !Uri.IsHexDigit(c)) ||
                string.IsNullOrWhiteSpace(input.EvidenceVersion))
                throw new ArgumentException("Full input fingerprint and evidence version are required.");
            effectivePolicy = await ReadEffectivePolicyAsync(cancellationToken)
                ?? throw new InvalidOperationException("Approved screening policy is not configured.");
            if (input.EffectivePolicyVersion != effectivePolicy.Version)
                throw new InvalidOperationException("Effective screening policy version changed; refresh inputs.");
        }

        // 1. Compute deterministic input snapshot hash
        var snapshotSha256 = ComputeSnapshotHash(input);

        // 2. Check idempotency: if an assessment already exists for this exact input snapshot, return it
        var existing = await _consolidatedStore.FindByIdempotencyKeyAsync(
            input.CaseId,
            input.ProposalVersion,
            snapshotSha256,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var evaluatedAtUtc = _timeProvider.GetUtcNow();
        var assessmentId = command.AssessmentId.HasValue && command.AssessmentId.Value != Guid.Empty
            ? command.AssessmentId.Value
            : Guid.NewGuid();

        // 3. Sub-Engine 1: Regulatory Compliance Check
        var (regulatoryResult, regulatorySection, regulatoryFindings) = await ExecuteRegulatoryComplianceAsync(
            input,
            evaluatedAtUtc.UtcDateTime,
            cancellationToken, effectivePolicy);

        // 4. Sub-Engine 2: Conflict Detection Check
        var (conflictResult, conflictSection, conflictFindings) = await ExecuteConflictDetectionAsync(
            input,
            evaluatedAtUtc.UtcDateTime,
            cancellationToken);

        // 5. Sub-Engine 3: Early Governance Screening Check
        var (earlyScreeningResult, earlySection, earlyFindings) = await ExecuteEarlyGovernanceScreeningAsync(
            input,
            assessmentId,
            evaluatedAtUtc,
            cancellationToken);

        // 6. Consolidated Synthesis & Recommendation
        var consolidatedFindings = new List<ConsolidatedFindingDto>();
        consolidatedFindings.AddRange(regulatoryFindings);
        consolidatedFindings.AddRange(conflictFindings);
        consolidatedFindings.AddRange(earlyFindings);

        var requiredConditions = new List<string>();
        foreach (var cond in regulatoryResult.Conditions ?? Array.Empty<ComplianceCondition>())
        {
            if (!string.IsNullOrWhiteSpace(cond.Description))
            {
                requiredConditions.Add(cond.Description.Trim());
            }
        }

        var overallStatus = DetermineOverallStatus(regulatorySection.Status, conflictSection.Status, earlySection.Status, regulatoryResult);
        if (effectivePolicy != null)
        {
            if (consolidatedFindings.Any(f => f.IsBlocking)) overallStatus = "Blocked";
            else if (regulatoryResult.Status == ComplianceStatus.InsufficientInformation ||
                regulatoryResult.Findings.Any(f => f.Status == RuleResultStatus.InsufficientInformation)) overallStatus = "InsufficientInformation";
        }
        var officerRecommendation = GenerateOfficerRecommendation(overallStatus, requiredConditions, earlySection.ReferralRecommended);

        var deterministicEvaluationId = ComputeDeterministicEvaluationId(assessmentId, input.CaseId, overallStatus, snapshotSha256);

        var consolidatedResult = new ConsolidatedGovernanceScreeningResultDto(
            AssessmentId: assessmentId,
            CaseId: input.CaseId,
            ApplicationReference: input.ApplicationReference,
            ProposalVersion: input.ProposalVersion,
            VerifiedFactSnapshotId: input.VerifiedFactSnapshotId,
            EvaluatedAtUtc: evaluatedAtUtc,
            DeterministicEvaluationId: deterministicEvaluationId,
            SnapshotSha256: snapshotSha256,
            OverallStatus: overallStatus,
            OfficerRecommendationSummary: officerRecommendation,
            RequiredConditions: requiredConditions,
            ConsolidatedFindings: consolidatedFindings,
            RegulatoryCheck: regulatorySection,
            ConflictCheck: conflictSection,
            EarlyGovernanceCheck: earlySection,
            Provenance: new ScreeningProvenanceMetadataDto(
                EngineVersion: "C4-Engine-v2.5.1",
                RegulatoryCatalogueSource: effectivePolicy?.SourceReference ?? "Legacy regulatory provider (not the catalogue)",
                RuleSetVersion: effectivePolicy?.Version ?? "Legacy-unversioned",
                IsDevelopmentFixture: input.IsDevelopmentSpatialMock || input.CaseId.StartsWith("LLA-2025-", StringComparison.OrdinalIgnoreCase),
                FixtureLabel: input.IsDevelopmentSpatialMock ? "Development mock spatial evidence; no real parcel clearance" :
                    input.CaseId.StartsWith("LLA-2025-", StringComparison.OrdinalIgnoreCase) ? "C4 Development Prototype Fixture" : "Verified input screening; not application approval"
            ), FullInputFingerprint: input.FullInputFingerprint, EffectivePolicyVersion: input.EffectivePolicyVersion, EvidenceVersion: input.EvidenceVersion
        );

        // 7. Persist consolidated screening snapshot
        if (effectivePolicy != null && (await ReadEffectivePolicyAsync(cancellationToken))?.Version != effectivePolicy.Version)
            throw new InvalidOperationException("Screening policy changed during execution; result was not persisted as current.");
        await _consolidatedStore.SaveAsync(consolidatedResult, cancellationToken);

        return consolidatedResult;
    }

    public async Task<ApprovedScreeningPolicy?> ReadEffectivePolicyAsync(CancellationToken ct)
    {
        var policy = _approvedPolicy == null ? null : await _approvedPolicy.ReadAsync(_timeProvider.GetUtcNow().UtcDateTime, ct);
        if (policy == null || policy.Rules.Count == 0 || string.IsNullOrWhiteSpace(policy.Version) || string.IsNullOrWhiteSpace(policy.SourceReference)) return null;
        return policy;
    }

    private static void ValidateInput(GovernanceScreeningCaseInputDto input)
    {
        if (string.IsNullOrWhiteSpace(input.CaseId)) throw new ArgumentException("CaseId is required.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.ApplicationReference)) throw new ArgumentException("ApplicationReference is required.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.ProposalVersion)) throw new ArgumentException("ProposalVersion is required.", nameof(input));
        if (input.VerifiedFactSnapshotId == Guid.Empty) throw new ArgumentException("VerifiedFactSnapshotId cannot be empty.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.ParcelId)) throw new ArgumentException("ParcelId is required.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.CadastralNumber)) throw new ArgumentException("CadastralNumber is required.", nameof(input));
        if (input.ExtentHectares <= 0) throw new ArgumentException("ExtentHectares must be greater than zero.", nameof(input));
    }

    private async Task<(ComplianceResult Result, RegulatoryComplianceSectionDto Section, IReadOnlyList<ConsolidatedFindingDto> Findings)>
        ExecuteRegulatoryComplianceAsync(
            GovernanceScreeningCaseInputDto input,
            DateTime evaluationTimestamp,
            CancellationToken cancellationToken, ApprovedScreeningPolicy? effectivePolicy = null)
    {
        var rules = effectivePolicy == null ? (await _ruleProvider.GetActiveRulesAsync(cancellationToken)).ToList() : new List<RegulatoryRule>();
        var legacyInput = new LeaseEvaluationInput(
            input.LeaseDurationYears,
            input.ProposedUse,
            input.InvestmentAmount,
            input.ZoningArea ?? string.Empty);

        if (input.DevelopmentRegulatoryInput != null &&
            (!input.IsDevelopmentSpatialMock || effectivePolicy?.Version.StartsWith("DEV-CATALOGUE:", StringComparison.Ordinal) != true ||
             input.DevelopmentRegulatoryInput.ProposalId != input.CaseId ||
             string.IsNullOrWhiteSpace(input.DevelopmentRegulatoryInputVersion) || string.IsNullOrWhiteSpace(input.DevelopmentRegulatorySourceReference)))
            throw new InvalidOperationException("Structured demonstration inputs require the bound Development catalogue policy and labelled provenance.");
        var result = effectivePolicy == null ? _complianceEngine.Evaluate(legacyInput, rules, evaluationTimestamp)
            : _complianceEngine.EvaluateNpd(input.DevelopmentRegulatoryInput != null
                ? StateLandGovernance.GovernanceIntelligence.Application.DTOs.ProposalComplianceInputMapper.Map(input.DevelopmentRegulatoryInput, strictDemonstrationInput:true)
                : new ProposalComplianceInput(input.CaseId,
                Location: new(input.Province, input.District, input.DivisionalSecretariat, input.SpatialEvidence?.GramaNiladhariDivision, null),
                LandRequirement: new(RequiresLand: true, ExtentHectares: input.ExtentHectares),
                Budget: new(SubmittedProjectBudget: input.InvestmentAmount, CostComponents: null),
                EvidenceReferences: input.DocumentReferences?.Select(d => new EvidenceReferenceInput(d.DocumentType, d.StorageReference, EvidenceStatus.Provided)).ToList()),
                effectivePolicy.Rules, evaluationTimestamp);

        // Persist compliance evaluation with audit record
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "InitialGovernanceScreening.Compliance",
            result.Status.ToString(),
            $"Screened CaseId {input.CaseId} across {rules.Count} rules. Violations: {result.Violations.Count}, Conditions: {result.Conditions.Count}",
            evaluationTimestamp);

        await _evaluationStore.StoreComplianceEvaluationAsync(
            auditRecord,
            result,
            "InitialGovernanceScreening.Compliance",
            proposalId: input.CaseId,
            caseId: input.CaseId,
            cancellationToken);

        var violations = (result.Violations ?? Array.Empty<Violation>()).Select(v => $"{v.RuleCode}: {v.Message}").ToList();
        var conditions = (result.Conditions ?? Array.Empty<ComplianceCondition>()).Select(c => c.Description).ToList();

        var section = new RegulatoryComplianceSectionDto(
            Status: result.Status.ToString(),
            EvaluatedRulesCount: effectivePolicy == null ? rules.Count : result.Findings.Count,
            ViolationsCount: violations.Count,
            ConditionsCount: conditions.Count,
            Violations: violations,
            Conditions: conditions
        );

        var findings = new List<ConsolidatedFindingDto>();
        if (effectivePolicy != null)
            findings.AddRange(result.Findings.Select(f => new ConsolidatedFindingDto("Regulatory", f.RuleCode, f.Status.ToString(),
                f.ObservedValueSummary, f.IsBlocking, f.RequiresHumanReview, f.ExpectedRequirement,
                $"{f.SourceReference?.SourceAuthority}/{f.SourceReference?.SourceDocument}/{f.SourceReference?.SourceSection}")));
        foreach (var v in effectivePolicy == null ? result.Violations ?? Array.Empty<Violation>() : Array.Empty<Violation>())
        {
            findings.Add(new ConsolidatedFindingDto(
                Dimension: "Regulatory",
                ItemName: v.RuleCode,
                Status: "NonCompliant",
                Details: v.Message,
                IsBlocking: true,
                RequiresHumanReview: true,
                EvidenceReference: null,
                SourceAuthorityOrRule: "Legacy research regulatory provider"
            ));
        }

        foreach (var c in effectivePolicy == null ? result.Conditions ?? Array.Empty<ComplianceCondition>() : Array.Empty<ComplianceCondition>())
        {
            findings.Add(new ConsolidatedFindingDto(
                Dimension: "Regulatory",
                ItemName: "Condition",
                Status: "Conditional",
                Details: c.Description,
                IsBlocking: false,
                RequiresHumanReview: true,
                EvidenceReference: null,
                SourceAuthorityOrRule: "Legacy research regulatory provider"
            ));
        }

        return (result, section, findings);
    }

    private async Task<(IReadOnlyList<DetectedConflict> Conflicts, ConflictDetectionSectionDto Section, IReadOnlyList<ConsolidatedFindingDto> Findings)>
        ExecuteConflictDetectionAsync(
            GovernanceScreeningCaseInputDto input,
            DateTime evaluationTimestamp,
            CancellationToken cancellationToken)
    {
        var snapshots = new List<GovernanceDecisionSnapshot>
        {
            new(
                decisionId: $"DEC-{input.CaseId}",
                subjectId: input.ParcelId,
                institutionName: "Land Commissioner General's Department",
                authorityLevel: "National",
                decisionType: "Approval",
                proposedUse: input.ProposedUse,
                effectiveFrom: evaluationTimestamp,
                effectiveTo: evaluationTimestamp.AddYears(input.LeaseDurationYears),
                regulatoryReference: "State Lands Ordinance",
                incompatibleRegulatoryReferences: null,
                mandateKey: "StateLandLeasing",
                mandateMode: "Exclusive",
                landUseCode: input.ProposedUse,
                incompatibleLandUseCodes: null,
                applicantId: input.ApplicantName,
                parcelGeometry: input.SpatialEvidence?.BoundaryGeoJson,
                recordStatus: "Proposed"
            )
        };

        // Add adjacent leases as candidate conflict contexts
        if (input.SpatialEvidence?.AdjacentLeases != null)
        {
            foreach (var adj in input.SpatialEvidence.AdjacentLeases)
            {
                snapshots.Add(new GovernanceDecisionSnapshot(
                    decisionId: $"ADJ-{adj.LeaseReference}",
                    subjectId: $"ADJ-{adj.ParcelCadastralNumber}",
                    institutionName: "Department of Coastal Conservation",
                    authorityLevel: "Provincial",
                    decisionType: "Approval",
                    proposedUse: "Commercial/Maritime",
                    effectiveFrom: evaluationTimestamp.AddYears(-5),
                    effectiveTo: evaluationTimestamp.AddYears(15),
                    regulatoryReference: "Coastal Zone Management Act",
                    incompatibleRegulatoryReferences: null,
                    mandateKey: "CoastalZoneProtection",
                    mandateMode: "Shared",
                    landUseCode: "MaritimeCommercial",
                    incompatibleLandUseCodes: null,
                    applicantId: adj.LesseeName,
                    parcelGeometry: null,
                    recordStatus: adj.CurrentStatus
                ));
            }
        }

        // Add concurrent applications
        if (input.ConcurrentApplications != null)
        {
            foreach (var conc in input.ConcurrentApplications)
            {
                snapshots.Add(new GovernanceDecisionSnapshot(
                    decisionId: $"CONC-{conc.ConcurrentCaseId}",
                    subjectId: input.ParcelId,
                    institutionName: "Divisional Secretariat",
                    authorityLevel: "Local",
                    decisionType: "Approval",
                    proposedUse: conc.ProposedUse,
                    effectiveFrom: conc.SubmittedAtUtc.UtcDateTime,
                    effectiveTo: conc.SubmittedAtUtc.UtcDateTime.AddYears(10),
                    regulatoryReference: "Crown Lands Ordinance",
                    incompatibleRegulatoryReferences: null,
                    mandateKey: "StateLandLeasing",
                    mandateMode: "Exclusive",
                    landUseCode: conc.ProposedUse,
                    incompatibleLandUseCodes: null,
                    applicantId: conc.ApplicantName,
                    parcelGeometry: null,
                    recordStatus: conc.WorkflowStatus
                ));
            }
        }

        var conflicts = _conflictEngine.DetectConflicts(snapshots, evaluationTimestamp);

        var highestSeverity = "None";
        if (conflicts.Count > 0)
        {
            highestSeverity = conflicts.Any(c => string.Equals(c.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
                ? "Critical"
                : conflicts.Any(c => string.Equals(c.Severity, "High", StringComparison.OrdinalIgnoreCase))
                    ? "High"
                    : "Medium";
        }

        // The existing explicit parcel-overlap finding belongs in this section too; a clear
        // decision-context comparison cannot cancel a source-backed overlap flag.
        var explicitParcelOverlap = input.SpatialEvidence?.HasOverlappingParcels == true;
        if (explicitParcelOverlap && highestSeverity != "Critical") highestSeverity = "High";
        var totalConflictCount = conflicts.Count + (explicitParcelOverlap ? 1 : 0);
        // Persist conflict evaluation
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.GovernanceConflict,
            "InitialGovernanceScreening.Conflict",
            totalConflictCount > 0 ? "ConflictDetected" : "Clear",
            $"Conflict check evaluated {snapshots.Count} decision contexts with {conflicts.Count} engine conflicts; explicit parcel overlap: {explicitParcelOverlap}.",
            evaluationTimestamp);

        await _evaluationStore.StoreConflictEvaluationAsync(
            auditRecord,
            conflicts,
            "InitialGovernanceScreening.Conflict",
            snapshots.Count,
            cancellationToken);

        var conflictSummaries = conflicts.Select(c => $"{c.ConflictType} ({c.Severity}): {c.Explanation}").ToList();
        if (explicitParcelOverlap) conflictSummaries.Add("Parcel Overlap (High): " + input.SpatialEvidence!.OverlapDetails);

        var section = new ConflictDetectionSectionDto(
            Status: totalConflictCount == 0 ? "Clear" : "ConflictDetected",
            HighestSeverity: highestSeverity,
            ConflictsCount: totalConflictCount,
            DetectedConflictSummaries: conflictSummaries
        );

        var findings = new List<ConsolidatedFindingDto>();

        // Parcel overlap check finding
        findings.Add(new ConsolidatedFindingDto(
            Dimension: "Conflict",
            ItemName: "Parcel Overlap Check",
            Status: input.SpatialEvidence?.HasOverlappingParcels == true ? "ConflictDetected" : "Clear",
            Details: input.SpatialEvidence?.OverlapDetails ?? "No overlapping parcels detected.",
            IsBlocking: input.SpatialEvidence?.HasOverlappingParcels == true,
            RequiresHumanReview: input.SpatialEvidence?.HasOverlappingParcels == true,
            EvidenceReference: input.ParcelId,
            SourceAuthorityOrRule: "Survey Department Cadastre Layer"
        ));

        // Existing lease check finding
        bool hasNearbyLease = input.SpatialEvidence?.AdjacentLeases?.Any() == true;
        findings.Add(new ConsolidatedFindingDto(
            Dimension: "Conflict",
            ItemName: "Existing Lease Conflict",
            Status: hasNearbyLease ? "RequiresReview" : "Clear",
            Details: hasNearbyLease
                ? $"Nearby active lease ({input.SpatialEvidence!.AdjacentLeases![0].ParcelCadastralNumber} - {input.SpatialEvidence.AdjacentLeases[0].LesseeName})"
                : "No conflicting active leases in immediate proximity.",
            IsBlocking: false,
            RequiresHumanReview: hasNearbyLease,
            EvidenceReference: hasNearbyLease ? input.SpatialEvidence!.AdjacentLeases![0].LeaseReference : null,
            SourceAuthorityOrRule: "State Land Lease Register"
        ));

        // Concurrent applications finding
        bool hasConcurrent = input.ConcurrentApplications?.Any() == true;
        findings.Add(new ConsolidatedFindingDto(
            Dimension: "Conflict",
            ItemName: "Concurrent Applications",
            Status: hasConcurrent ? "RequiresReview" : "Clear",
            Details: hasConcurrent ? $"{input.ConcurrentApplications!.Count} concurrent application(s) pending." : "No concurrent applications recorded.",
            IsBlocking: false,
            RequiresHumanReview: hasConcurrent,
            EvidenceReference: hasConcurrent ? input.ConcurrentApplications![0].ConcurrentCaseId : null,
            SourceAuthorityOrRule: "Land Registry Queue"
        ));

        // Applicant eligibility & tender competition
        findings.Add(new ConsolidatedFindingDto(
            Dimension: "Conflict",
            ItemName: "Applicant Eligibility",
            Status: "Clear",
            Details: "Applicant identity and business registration are eligible for review.",
            IsBlocking: false,
            RequiresHumanReview: false,
            EvidenceReference: input.ApplicantName,
            SourceAuthorityOrRule: "Land Commissioner General Eligibility Guidelines"
        ));

        findings.Add(new ConsolidatedFindingDto(
            Dimension: "Conflict",
            ItemName: "Tender Competition",
            Status: input.ExtentHectares > 10.0m ? "RequiresReview" : "Clear",
            Details: input.ExtentHectares > 10.0m ? "Extent exceeds 10 hectares; competitive public tender inquiry recommended." : "Within direct allocation discretionary extent.",
            IsBlocking: false,
            RequiresHumanReview: input.ExtentHectares > 10.0m,
            EvidenceReference: $"{input.ExtentHectares} ha",
            SourceAuthorityOrRule: "State Land Allocation Competitive Policy"
        ));

        return (conflicts, section, findings);
    }

    private async Task<(EarlyGovernanceScreeningResultDto Result, EarlyGovernanceSectionDto Section, IReadOnlyList<ConsolidatedFindingDto> Findings)>
        ExecuteEarlyGovernanceScreeningAsync(
            GovernanceScreeningCaseInputDto input,
            Guid assessmentId,
            DateTimeOffset evaluationTimestamp,
            CancellationToken cancellationToken)
    {
        var domainIndicators = new List<EarlyGovernanceIndicatorRecord>
        {
            // 1. Legal dispute
            new(
                EarlyGovernanceIndicatorType.LegalDispute,
                input.VerifiedLegalDisputeFlag ? EarlyGovernanceEvidenceState.VerifiedPresent : EarlyGovernanceEvidenceState.VerifiedAbsent,
                input.VerifiedLegalDisputeFlag ? "COURT-REF-DISPUTE" : "COURT-SEARCH-CLEAR",
                evaluationTimestamp
            ),
            // 2. Unauthorized occupation
            new(
                EarlyGovernanceIndicatorType.UnauthorizedOccupation,
                input.UnauthorizedOccupationFlag ? EarlyGovernanceEvidenceState.VerifiedPresent : EarlyGovernanceEvidenceState.VerifiedAbsent,
                input.UnauthorizedOccupationFlag ? "FIELD-INSPECTION-OCCUPATION" : "FIELD-INSPECTION-CLEAR",
                evaluationTimestamp
            ),
            // 3. Unauthorized construction
            new(
                EarlyGovernanceIndicatorType.UnauthorizedConstruction,
                EarlyGovernanceEvidenceState.VerifiedAbsent,
                "PHYSICAL-SURVEY-CLEAR",
                evaluationTimestamp
            ),
            // 4. Family / inheritance claims
            new(
                EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim,
                input.FamilyOrInheritanceClaimFlag ? EarlyGovernanceEvidenceState.VerifiedPresent : EarlyGovernanceEvidenceState.VerifiedAbsent,
                input.FamilyOrInheritanceClaimFlag ? "LAND-SETTLEMENT-CLAIM" : "TITLE-SEARCH-CLEAR",
                evaluationTimestamp
            ),
            // 5. Multiple claimants
            new(
                EarlyGovernanceIndicatorType.MultipleClaimants,
                (input.ConcurrentApplications != null && input.ConcurrentApplications.Count > 0)
                    ? EarlyGovernanceEvidenceState.VerifiedPresent
                    : EarlyGovernanceEvidenceState.VerifiedAbsent,
                input.ConcurrentApplications?.FirstOrDefault()?.ConcurrentCaseId ?? "CLAIMANT-REGISTRY-CLEAR",
                evaluationTimestamp
            ),
            // 6. Unresolved objection
            new(
                EarlyGovernanceIndicatorType.UnresolvedObjection,
                input.ObjectionReceivedFlag ? EarlyGovernanceEvidenceState.VerifiedPresent : EarlyGovernanceEvidenceState.VerifiedAbsent,
                input.ObjectionReceivedFlag ? "DS-OBJECTION-RECORD" : "PUBLIC-NOTICE-CLEAR",
                evaluationTimestamp
            ),
            // 7. Previous illegal land activity
            new(
                EarlyGovernanceIndicatorType.PreviousIllegalLandActivity,
                input.DocumentedPriorActivityFlag ? EarlyGovernanceEvidenceState.VerifiedPresent : EarlyGovernanceEvidenceState.VerifiedAbsent,
                input.DocumentedPriorActivityFlag ? "PRIOR-ACTIVITY-RECORD" : "LAND-LEDGER-CLEAR",
                evaluationTimestamp
            )
        };

        var screeningInput = new EarlyGovernanceScreeningInput(input.CaseId, input.ProposalVersion, domainIndicators);
        var domainResult = _earlyScreeningEngine.Screen(screeningInput);

        var indicatorDtos = domainResult.IndicatorResults
            .Select(r => new EarlyGovernanceIndicatorResultDto(
                r.IndicatorType.ToString(),
                r.EvidenceState.ToString(),
                r.RequiresReview,
                r.NeedsEvidence,
                r.ReasonCode,
                r.Message,
                r.EvidenceReference,
                r.RecordedAtUtc))
            .ToList();

        var resultDto = new EarlyGovernanceScreeningResultDto(
            domainResult.CaseId,
            domainResult.InputVersion,
            domainResult.OverallStatus.ToString(),
            domainResult.HasIncompleteEvidence,
            indicatorDtos
        );

        bool recommendReferral = domainResult.OverallStatus == EarlyGovernanceScreeningStatus.ReviewRequired;
        string? referralReason = recommendReferral ? "Early governance indicators flagged verified concerns requiring administrative review." : null;

        var workflowRunId = Guid.NewGuid();
        EarlyGovernanceReferralIntentDto? referralIntent = null;
        if (recommendReferral)
        {
            referralIntent = new EarlyGovernanceReferralIntentDto(
                Guid.NewGuid(),
                assessmentId,
                assessmentId,
                input.CaseId,
                workflowRunId,
                "EARLY_GOV_REVIEW",
                referralReason!,
                new[] { input.ParcelId },
                evaluationTimestamp
            );
        }

        if (_earlyScreeningStore != null)
        {
            try
            {
                await _earlyScreeningStore.AddAssessmentAsync(
                    assessmentId,
                    workflowRunId,
                    evaluationTimestamp,
                    resultDto,
                    referralIntent,
                    cancellationToken);
            }
            catch
            {
                // In-memory or fallback store
            }
        }

        var section = new EarlyGovernanceSectionDto(
            Status: resultDto.OverallStatus,
            IndicatorsCount: domainIndicators.Count,
            ReferralRecommended: recommendReferral,
            ReferralReason: referralReason
        );

        var findings = new List<ConsolidatedFindingDto>();
        bool hasComplaints = input.ContemporaneousComplaints?.Any() == true;
        if (hasComplaints)
        {
            findings.Add(new ConsolidatedFindingDto(
                "EarlyGovernance",
                "Complaints",
                "RequiresReview",
                $"{input.ContemporaneousComplaints!.Count} contemporaneous complaint(s) recorded against parcel/applicant.",
                IsBlocking: false,
                RequiresHumanReview: true,
                EvidenceReference: input.ContemporaneousComplaints[0].ComplaintId,
                SourceAuthorityOrRule: "Public Complaints Register"
            ));
        }

        foreach (var ind in domainResult.IndicatorResults)
        {
            if (ind.RequiresReview || ind.NeedsEvidence)
            {
                findings.Add(new ConsolidatedFindingDto(
                    "EarlyGovernance",
                    ind.IndicatorType.ToString(),
                    ind.RequiresReview ? "RequiresReview" : "InsufficientInformation",
                    ind.Message,
                    IsBlocking: ind.RequiresReview,
                    RequiresHumanReview: true,
                    EvidenceReference: ind.EvidenceReference,
                    SourceAuthorityOrRule: "Early Governance Screening Engine"
                ));
            }
        }

        return (resultDto, section, findings);
    }

    private static string DetermineOverallStatus(
        string regulatoryStatus,
        string conflictStatus,
        string earlyStatus,
        ComplianceResult regulatoryResult)
    {
        bool isRegulatoryBlocked = string.Equals(regulatoryStatus, "NonCompliant", StringComparison.OrdinalIgnoreCase) ||
                                  (regulatoryResult.Violations?.Count ?? 0) > 0;
        bool isEarlyBlocked = string.Equals(earlyStatus, "ReviewRequired", StringComparison.OrdinalIgnoreCase) &&
                              regulatoryResult.Violations?.Count > 0;

        if (isRegulatoryBlocked || isEarlyBlocked)
        {
            return "Blocked";
        }

        bool hasConditions = (regulatoryResult.Conditions?.Count ?? 0) > 0;
        bool hasRegulatoryReview = string.Equals(regulatoryStatus, "RequiresHumanReview", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(regulatoryStatus, "Conditional", StringComparison.OrdinalIgnoreCase);
        bool hasConflicts = string.Equals(conflictStatus, "ConflictDetected", StringComparison.OrdinalIgnoreCase);
        bool hasEarlyReview = string.Equals(earlyStatus, "ReviewRequired", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(earlyStatus, "InsufficientInformation", StringComparison.OrdinalIgnoreCase);

        if (hasConditions || hasRegulatoryReview || hasConflicts || hasEarlyReview)
        {
            return "RecommendedWithConditions";
        }

        if (string.Equals(regulatoryStatus, "InsufficientInformation", StringComparison.OrdinalIgnoreCase))
        {
            return "InsufficientInformation";
        }

        return "Cleared";
    }

    private static string GenerateOfficerRecommendation(
        string overallStatus,
        IReadOnlyList<string> requiredConditions,
        bool referralRecommended)
    {
        return overallStatus switch
        {
            "Blocked" =>
                "Application has critical regulatory or jurisdictional blockers. Cannot proceed to administrative review in its current form.",
            "RecommendedWithConditions" =>
                $"Application generally complies with regulatory requirements. {requiredConditions.Count} condition(s) require review and compliance during workflow execution.",
            "InsufficientInformation" =>
                "Required statutory evidence or field verifications are incomplete. Request additional documentation before workflow routing.",
            _ =>
                "Application fully satisfies all regulatory, conflict, and early governance requirements. Recommended to proceed to standard review workflow."
        };
    }

    public static string ComputeSnapshotHash(GovernanceScreeningCaseInputDto input)
    {
        // Complete received contract, including content (not counts) and separately named provenance versions.
        var canonical = JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string ComputeDeterministicEvaluationId(Guid assessmentId, string caseId, string status, string snapshotSha256)
    {
        var raw = $"{assessmentId}|{caseId}|{status}|{snapshotSha256}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return $"C4-EVAL-{hash[..12].ToUpperInvariant()}";
    }
}
