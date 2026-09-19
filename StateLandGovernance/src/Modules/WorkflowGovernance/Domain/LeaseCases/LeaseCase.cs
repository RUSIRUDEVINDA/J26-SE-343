namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.Events;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.Events;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Domain.Screening.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;

public sealed class LeaseCase
{
    public const string ProposalReviewCapability = "ProposalReviewer";

    public LeaseCaseId Id { get; }
    public string ApplicationReference { get; }
    public Guid CreatedByActorId { get; }
    public DateTime CreatedAt { get; }
    public int Revision { get; private set; }
    public LeaseProposalIntake? ProposalIntake { get; private set; }
    public Guid CurrentVerifiedFactSnapshotId { get; private set; }
    public ScreeningResult? LatestScreening { get; private set; }
    public WorkflowPlan? ActiveWorkflowPlan { get; private set; }

    private readonly List<WorkflowPlan> _workflowPlanHistory = new();
    public IReadOnlyCollection<WorkflowPlan> WorkflowPlanHistory => _workflowPlanHistory.AsReadOnly();

    private readonly List<ApprovalCondition> _approvalConditions = new();
    public IReadOnlyCollection<ApprovalCondition> ApprovalConditions => _approvalConditions.AsReadOnly();

    private readonly List<DocumentSubmissionRequirement> _documentSubmissionRequirements = new();
    public IReadOnlyCollection<DocumentSubmissionRequirement> DocumentSubmissionRequirements => _documentSubmissionRequirements.AsReadOnly();

    private readonly List<RequirementAssessmentResult> _assessmentHistory = new();
    public IReadOnlyCollection<RequirementAssessmentResult> AssessmentHistory => _assessmentHistory.AsReadOnly();

    private readonly List<ProposalContentCompletenessResult> _proposalContentAssessments = new();
    public IReadOnlyCollection<ProposalContentCompletenessResult> ProposalContentAssessmentHistory => _proposalContentAssessments.AsReadOnly();
    private int _proposalContentSequenceCounter = 0;

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public LeaseCase(
        LeaseCaseId id,
        string applicationReference,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot)
        : this(id, applicationReference, actorId, actionTime, authoritySnapshot, null, Guid.Empty)
    {
    }

    public LeaseCase(
        LeaseCaseId id,
        string applicationReference,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot,
        LeaseProposalIntake? proposalIntake)
        : this(id, applicationReference, actorId, actionTime, authoritySnapshot, proposalIntake, Guid.Empty)
    {
    }

    public LeaseCase(
        LeaseCaseId id,
        string applicationReference,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot,
        LeaseProposalIntake? proposalIntake,
        Guid currentVerifiedFactSnapshotId)
    {
        if (id == default || id.Value == Guid.Empty)
        {
            throw new InvalidLeaseCaseException("LeaseCaseId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(applicationReference))
        {
            throw new InvalidLeaseCaseException("ApplicationReference cannot be null, empty, or whitespace.");
        }

        if (actorId == Guid.Empty)
        {
            throw new InvalidLeaseCaseException("ActorId cannot be empty.");
        }

        if (authoritySnapshot == null)
        {
            throw new InvalidLeaseCaseException("VerifiedAuthoritySnapshot is required.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, id.Value.ToString());
        
        authoritySnapshot.EnsureAuthorizes(actorId, "LeaseInitiator", requiredScope, actionTime);

        Id = id;
        ApplicationReference = applicationReference;
        CreatedByActorId = actorId;
        CreatedAt = actionTime;
        ProposalIntake = proposalIntake;
        CurrentVerifiedFactSnapshotId = currentVerifiedFactSnapshotId;
        Revision = 1;

        _domainEvents.Add(new LeaseCaseInitialized(
            Guid.NewGuid(),
            actionTime,
            id,
            applicationReference,
            actorId,
            Revision
        ));
    }

    public void RecordProposalIntake(
        LeaseProposalIntake intake,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (intake == null)
        {
            throw new InvalidProposalIntakeException("LeaseProposalIntake cannot be null.");
        }

        if (authoritySnapshot == null)
        {
            throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Id.Value.ToString());
        authoritySnapshot.EnsureAuthorizes(actorId, "LeaseInitiator", requiredScope, actionTime);

        ProposalIntake = intake;
        Revision++;
    }

    public void RecordAssessmentResult(RequirementAssessmentResult result)
    {
        if (result == null)
        {
            throw new InvalidRequirementAssessmentException("RequirementAssessmentResult cannot be null.");
        }

        if (!result.LeaseCaseId.Equals(Id))
        {
            throw new InvalidRequirementAssessmentException("Assessment result LeaseCaseId does not match this lease case.");
        }

        if (_assessmentHistory.Any(a => a.Id.Equals(result.Id)))
        {
            throw new InvalidRequirementAssessmentException($"Assessment result with ID '{result.Id}' has already been recorded for this lease case.");
        }

        var previous = _assessmentHistory.LastOrDefault(a => a.Subject == result.Subject);
        _assessmentHistory.Add(result);

        if (previous != null)
        {
            _domainEvents.Add(new RequirementReassessed(
                Guid.NewGuid(),
                result.AssessedAt,
                Id,
                result.Subject,
                result.Outcome,
                previous.MatchedPolicyVersion,
                result.MatchedPolicyVersion
            ));
        }
        else
        {
            if (result.RequiresHumanConfirmation)
            {
                _domainEvents.Add(new RequirementAssessmentHumanReviewRequired(
                    Guid.NewGuid(),
                    result.AssessedAt,
                    Id,
                    result.Subject,
                    result.Explanation,
                    result.MatchedPolicyId,
                    result.MatchedPolicyVersion
                ));
            }
            else
            {
                _domainEvents.Add(new RequirementAssessmentCompleted(
                    Guid.NewGuid(),
                    result.AssessedAt,
                    Id,
                    result.Subject,
                    result.Outcome,
                    result.MatchedPolicyId,
                    result.MatchedPolicyVersion,
                    result.RequiresHumanConfirmation
                ));
            }
        }
    }

    public RequirementAssessmentResult? GetLatestAssessment(AssessmentSubject subject)
    {
        return _assessmentHistory.LastOrDefault(a => a.Subject == subject);
    }

    public void RecordProposalContentAssessment(ProposalContentCompletenessResult result)
    {
        if (result == null)
        {
            throw new InvalidProposalContentAssessmentException("ProposalContentCompletenessResult cannot be null.");
        }

        if (!result.LeaseCaseId.Equals(Id))
        {
            throw new InvalidProposalContentAssessmentException("Assessment result LeaseCaseId does not match this lease case.");
        }

        if (result.IsConfirmed ||
            result.ConfirmedByActorId.HasValue ||
            result.ConfirmedAtUtc.HasValue ||
            result.ConfirmationNotes != null ||
            result.ConfirmedResultOfId.HasValue ||
            result.SupersedesResultId.HasValue ||
            result.CorrectionReason != null ||
            result.CorrectionEvidenceReference != null)
        {
            throw new InvalidProposalContentReviewException(
                "RecordProposalContentAssessment accepts only proposed, unreviewed assessment results. " +
                "Confirmed and corrected assessment results must enter aggregate history through authorised confirmation or correction methods.");
        }

        if (_proposalContentAssessments.Any(a => a.Id.Equals(result.Id)))
        {
            throw new InvalidProposalContentAssessmentException($"Assessment result with ID '{result.Id}' has already been recorded for this lease case.");
        }

        var previous = _proposalContentAssessments
            .Where(a => a.SourceBinding.TemplateId.Equals(result.SourceBinding.TemplateId))
            .OrderByDescending(a => a.SequenceNumber)
            .FirstOrDefault();

        var nextSequence = checked(_proposalContentSequenceCounter + 1);
        var storedResult = result.WithSequenceNumber(nextSequence);

        IDomainEvent domainEvent;
        if (previous != null)
        {
            domainEvent = new ProposalContentReassessed(
                Guid.NewGuid(),
                result.SourceBinding.AssessedAt,
                Id,
                previous.Id,
                result.Id,
                previous.Outcome,
                result.Outcome,
                previous.SourceBinding.TemplateVersion,
                result.SourceBinding.TemplateVersion
            );
        }
        else if (result.Outcome == ProposalContentCompletenessOutcome.HumanReviewRequired)
        {
            domainEvent = new ProposalContentHumanReviewRequired(
                Guid.NewGuid(),
                result.SourceBinding.AssessedAt,
                Id,
                result.Id,
                result.SourceBinding.TemplateId,
                result.SourceBinding.TemplateVersion,
                result.Explanations
            );
        }
        else
        {
            domainEvent = new ProposalContentCompletenessAssessed(
                Guid.NewGuid(),
                result.SourceBinding.AssessedAt,
                Id,
                result.Id,
                result.Outcome,
                result.SourceBinding.TemplateId,
                result.SourceBinding.TemplateVersion,
                result.SourceBinding.DocumentVersionId,
                result.SourceBinding.DocumentChecksum
            );
        }

        _proposalContentSequenceCounter = nextSequence;
        _proposalContentAssessments.Add(storedResult);
        _domainEvents.Add(domainEvent);
    }

    public ProposalContentCompletenessResult ConfirmProposalContentAssessment(
        ProposalContentAssessmentResultId resultId,
        VerifiedAuthoritySnapshot authoritySnapshot,
        Guid actorId,
        DateTime actionTime,
        string? notes)
    {
        if (authoritySnapshot == null)
        {
            throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required.");
        }

        var existing = _proposalContentAssessments.FirstOrDefault(a => a.Id.Equals(resultId));
        if (existing == null)
        {
            throw new InvalidProposalContentAssessmentException($"Proposal content assessment result with ID '{resultId}' was not found.");
        }

        if (existing.IsConfirmed)
        {
            throw new InvalidProposalContentReviewException("Assessment result has already been confirmed.");
        }

        if (_proposalContentAssessments.Any(a => a.ConfirmedResultOfId.HasValue && a.ConfirmedResultOfId.Value.Equals(resultId)))
        {
            throw new InvalidProposalContentReviewException("Assessment result has already been confirmed.");
        }

        if (_proposalContentAssessments.Any(a => a.SupersedesResultId.HasValue && a.SupersedesResultId.Value.Equals(resultId)))
        {
            throw new InvalidProposalContentReviewException("Cannot confirm an already superseded assessment result.");
        }

        if (actionTime.Kind != DateTimeKind.Utc)
        {
            throw new InvalidProposalContentReviewException("ConfirmedAtUtc must be UTC.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Id.Value.ToString());
        authoritySnapshot.EnsureAuthorizes(actorId, ProposalReviewCapability, requiredScope, actionTime);

        var nextSequence = checked(_proposalContentSequenceCounter + 1);
        var confirmedResultId = ProposalContentAssessmentResultId.New();
        var confirmedResult = existing.WithConfirmation(confirmedResultId, actorId, actionTime, notes, nextSequence);

        var domainEvent = new ProposalContentAssessmentConfirmed(
            Guid.NewGuid(),
            actionTime,
            Id,
            confirmedResult.Id,
            actorId,
            actionTime,
            notes
        );

        _proposalContentSequenceCounter = nextSequence;
        _proposalContentAssessments.Add(confirmedResult);
        _domainEvents.Add(domainEvent);

        return confirmedResult;
    }

    public ProposalContentCompletenessResult CorrectProposalContentAssessment(
        ProposalContentAssessmentResultId resultId,
        VerifiedAuthoritySnapshot authoritySnapshot,
        Guid actorId,
        DateTime actionTime,
        string reason,
        IEnumerable<ProposalRequirementObservation> correctedObservations,
        string? evidenceReference,
        ProposalTemplate template)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidProposalContentReviewException("Correction reason is required.");
        }

        if (string.IsNullOrWhiteSpace(evidenceReference))
        {
            throw new InvalidProposalContentReviewException("Correction evidence reference is required.");
        }

        if (actionTime.Kind != DateTimeKind.Utc)
        {
            throw new InvalidProposalContentReviewException("ConfirmedAtUtc must be UTC.");
        }

        if (authoritySnapshot == null)
        {
            throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required.");
        }

        if (template == null)
        {
            throw new InvalidProposalTemplateException("ProposalTemplate cannot be null for correction.");
        }

        if (correctedObservations == null)
        {
            throw new InvalidProposalObservationException("Corrected observations collection cannot be null.");
        }

        var original = _proposalContentAssessments.FirstOrDefault(a => a.Id.Equals(resultId));
        if (original == null)
        {
            throw new InvalidProposalContentAssessmentException($"Proposal content assessment result with ID '{resultId}' was not found.");
        }

        if (_proposalContentAssessments.Any(a => a.SupersedesResultId.HasValue && a.SupersedesResultId.Value.Equals(resultId)))
        {
            throw new InvalidProposalContentReviewException($"Assessment result with ID '{resultId}' has already been superseded by a correction.");
        }

        if (!original.SourceBinding.MatchesTemplate(template.Id, template.Version))
        {
            throw new InvalidProposalContentReviewException("Correction must use the exact same template ID and version as the original assessment.");
        }

        var templateSnapshot = template.CreateSnapshot();
        if (!string.Equals(templateSnapshot.DefinitionDigest, original.TemplateSnapshot.DefinitionDigest, StringComparison.Ordinal))
        {
            throw new InvalidProposalContentReviewException(
                $"Correction template definition digest '{templateSnapshot.DefinitionDigest}' does not match original template digest '{original.TemplateSnapshot.DefinitionDigest}'. A changed template definition requires a new reassessment.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Id.Value.ToString());
        authoritySnapshot.EnsureAuthorizes(actorId, ProposalReviewCapability, requiredScope, actionTime);

        var nextSequence = checked(_proposalContentSequenceCounter + 1);

        var evaluated = ProposalContentCompletenessEvaluator.Evaluate(original.SourceBinding, template, correctedObservations);

        var correctedResultId = ProposalContentAssessmentResultId.New();
        var correctedResult = new ProposalContentCompletenessResult(
            correctedResultId,
            Id,
            original.SourceBinding,
            evaluated.TemplateSnapshot,
            evaluated.Outcome,
            evaluated.SatisfiedMandatory,
            evaluated.MissingMandatory,
            evaluated.ReviewRequiredMandatory,
            evaluated.MissingOptional,
            evaluated.SatisfiedOptional,
            evaluated.Explanations,
            evaluated.UnresolvedOptional,
            isConfirmed: true,
            confirmedByActorId: actorId,
            confirmedAtUtc: actionTime,
            confirmationNotes: reason,
            confirmedResultOfId: null,
            supersedesResultId: original.Id,
            correctionReason: reason,
            correctionEvidenceReference: evidenceReference,
            sequenceNumber: nextSequence
        );

        var domainEvent = new ProposalContentAssessmentCorrected(
            Guid.NewGuid(),
            actionTime,
            Id,
            original.Id,
            correctedResult.Id,
            actorId,
            actionTime,
            reason
        );

        _proposalContentSequenceCounter = nextSequence;
        _proposalContentAssessments.Add(correctedResult);
        _domainEvents.Add(domainEvent);

        return correctedResult;
    }

    /// <summary>
    /// Returns the latest recorded proposal content assessment for the given template ID across history.
    /// This is an audit/historical query only and does NOT claim currentness.
    /// To evaluate currentness, use <see cref="GetCurrentProposalContentAssessment(ProposalSourceBinding, string)"/>
    /// or <see cref="IsAssessmentCurrent(ProposalContentAssessmentResultId, ProposalSourceBinding, string)"/>.
    /// </summary>
    public ProposalContentCompletenessResult? GetLatestRecordedProposalContentAssessment(ProposalTemplateId templateId)
    {
        return _proposalContentAssessments
            .Where(a => a.SourceBinding.TemplateId.Equals(templateId))
            .OrderByDescending(a => a.SequenceNumber)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns the latest recorded proposal content assessment across history.
    /// This is an audit/historical query only and does NOT claim currentness.
    /// To evaluate currentness, use <see cref="GetCurrentProposalContentAssessment(ProposalSourceBinding, string)"/>
    /// or <see cref="IsAssessmentCurrent(ProposalContentAssessmentResultId, ProposalSourceBinding, string)"/>.
    /// </summary>
    public ProposalContentCompletenessResult? GetLatestRecordedProposalContentAssessment()
    {
        return _proposalContentAssessments
            .OrderByDescending(a => a.SequenceNumber)
            .FirstOrDefault();
    }

    /// <summary>
    /// Alias for <see cref="GetLatestRecordedProposalContentAssessment(ProposalTemplateId)"/>.
    /// This is an audit/historical query only and does NOT claim currentness.
    /// </summary>
    public ProposalContentCompletenessResult? GetLatestProposalContentAssessment(ProposalTemplateId templateId)
    {
        return GetLatestRecordedProposalContentAssessment(templateId);
    }

    /// <summary>
    /// Alias for <see cref="GetLatestRecordedProposalContentAssessment()"/>.
    /// This is an audit/historical query only and does NOT claim currentness.
    /// </summary>
    public ProposalContentCompletenessResult? GetLatestProposalContentAssessment()
    {
        return GetLatestRecordedProposalContentAssessment();
    }

    /// <summary>
    /// Returns the current applicable proposal content assessment for the given source binding and expected template digest.
    /// An assessment is current if and only if:
    /// 1. It belongs to this lease case.
    /// 2. It exactly matches the complete source binding (case, document, version, checksum, template ID, template version).
    /// 3. Its template snapshot has an exact match for the expected template-definition digest.
    /// 4. It has not been superseded by a correction.
    /// 5. It is not an unconfirmed original that has subsequently been confirmed.
    /// If multiple assessments match, the one with the highest sequence number is returned.
    /// </summary>
    public ProposalContentCompletenessResult? GetCurrentProposalContentAssessment(
        ProposalSourceBinding currentSourceBinding,
        string expectedTemplateDigest)
    {
        if (currentSourceBinding == null)
        {
            throw new InvalidProposalSourceBindingException("Current ProposalSourceBinding cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(expectedTemplateDigest))
        {
            throw new InvalidProposalTemplateException("Expected template digest is mandatory and cannot be empty.");
        }

        var normalizedDigest = expectedTemplateDigest.Trim();

        var supersededIds = _proposalContentAssessments
            .Where(a => a.SupersedesResultId.HasValue)
            .Select(a => a.SupersedesResultId!.Value)
            .ToHashSet();

        var confirmedOriginalIds = _proposalContentAssessments
            .Where(a => a.ConfirmedResultOfId.HasValue)
            .Select(a => a.ConfirmedResultOfId!.Value)
            .ToHashSet();

        return _proposalContentAssessments
            .Where(a => a.LeaseCaseId.Equals(Id) &&
                        a.SourceBinding.MatchesSource(currentSourceBinding) &&
                        string.Equals(a.TemplateSnapshot.DefinitionDigest, normalizedDigest, StringComparison.Ordinal) &&
                        !supersededIds.Contains(a.Id) &&
                        !confirmedOriginalIds.Contains(a.Id))
            .OrderByDescending(a => a.SequenceNumber)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns the current applicable proposal content assessment for the given source binding and expected template snapshot.
    /// Obtains the expected template-definition digest internally from the mandatory snapshot.
    /// </summary>
    public ProposalContentCompletenessResult? GetCurrentProposalContentAssessment(
        ProposalSourceBinding currentSourceBinding,
        ProposalTemplateSnapshot currentTemplateSnapshot)
    {
        if (currentTemplateSnapshot == null)
        {
            throw new InvalidProposalTemplateException("Current ProposalTemplateSnapshot cannot be null.");
        }

        return GetCurrentProposalContentAssessment(currentSourceBinding, currentTemplateSnapshot.DefinitionDigest);
    }

    /// <summary>
    /// Evaluates whether the given proposal content assessment result is current for the specified source binding and expected template digest.
    /// Note: Currentness alone does not establish completeness-gate eligibility. A future completeness gate must additionally require
    /// that the assessment is confirmed (<see cref="ProposalContentCompletenessResult.IsConfirmed"/>) and complete (<see cref="ProposalContentCompletenessResult.Outcome"/> == Complete).
    /// </summary>
    public bool IsAssessmentCurrent(
        ProposalContentAssessmentResultId resultId,
        ProposalSourceBinding currentBinding,
        string expectedTemplateDigest)
    {
        var current = GetCurrentProposalContentAssessment(currentBinding, expectedTemplateDigest);
        return current != null && current.Id.Equals(resultId);
    }

    /// <summary>
    /// Evaluates whether the given proposal content assessment result is current for the specified source binding and expected template snapshot.
    /// Note: Currentness alone does not establish completeness-gate eligibility. A future completeness gate must additionally require
    /// that the assessment is confirmed (<see cref="ProposalContentCompletenessResult.IsConfirmed"/>) and complete (<see cref="ProposalContentCompletenessResult.Outcome"/> == Complete).
    /// </summary>
    public bool IsAssessmentCurrent(
        ProposalContentAssessmentResultId resultId,
        ProposalSourceBinding currentBinding,
        ProposalTemplateSnapshot currentTemplateSnapshot)
    {
        if (currentTemplateSnapshot == null)
        {
            throw new InvalidProposalTemplateException("Current ProposalTemplateSnapshot cannot be null.");
        }

        return IsAssessmentCurrent(resultId, currentBinding, currentTemplateSnapshot.DefinitionDigest);
    }

    /// <summary>
    /// Evaluates whether the given proposal content assessment result is current for the specified source coordinates and expected template digest.
    /// Note: Currentness alone does not establish completeness-gate eligibility. A future completeness gate must additionally require
    /// that the assessment is confirmed (<see cref="ProposalContentCompletenessResult.IsConfirmed"/>) and complete (<see cref="ProposalContentCompletenessResult.Outcome"/> == Complete).
    /// </summary>
    public bool IsAssessmentCurrent(
        ProposalContentAssessmentResultId resultId,
        DocumentVersionId currentDocumentVersionId,
        DocumentChecksum currentChecksum,
        ProposalTemplateId currentTemplateId,
        string currentTemplateVersion,
        string expectedTemplateDigest)
    {
        if (string.IsNullOrWhiteSpace(expectedTemplateDigest))
        {
            throw new InvalidProposalTemplateException("Expected template digest is mandatory and cannot be empty.");
        }

        var target = _proposalContentAssessments.FirstOrDefault(a => a.Id.Equals(resultId));
        if (target == null) return false;

        if (_proposalContentAssessments.Any(a => a.SupersedesResultId.HasValue && a.SupersedesResultId.Value.Equals(resultId)))
        {
            return false;
        }

        if (!target.IsCurrentFor(currentDocumentVersionId, currentChecksum, currentTemplateId, currentTemplateVersion, expectedTemplateDigest))
        {
            return false;
        }

        var current = GetCurrentProposalContentAssessment(target.SourceBinding, expectedTemplateDigest);
        return current != null && current.Id.Equals(resultId);
    }

    /// <summary>
    /// Evaluates whether the given proposal content assessment result is current for the specified source coordinates and expected template snapshot.
    /// Note: Currentness alone does not establish completeness-gate eligibility. A future completeness gate must additionally require
    /// that the assessment is confirmed (<see cref="ProposalContentCompletenessResult.IsConfirmed"/>) and complete (<see cref="ProposalContentCompletenessResult.Outcome"/> == Complete).
    /// </summary>
    public bool IsAssessmentCurrent(
        ProposalContentAssessmentResultId resultId,
        DocumentVersionId currentDocumentVersionId,
        DocumentChecksum currentChecksum,
        ProposalTemplateId currentTemplateId,
        string currentTemplateVersion,
        ProposalTemplateSnapshot currentTemplateSnapshot)
    {
        if (currentTemplateSnapshot == null)
        {
            throw new InvalidProposalTemplateException("Current ProposalTemplateSnapshot cannot be null.");
        }

        return IsAssessmentCurrent(
            resultId,
            currentDocumentVersionId,
            currentChecksum,
            currentTemplateId,
            currentTemplateVersion,
            currentTemplateSnapshot.DefinitionDigest);
    }

    public void SetCurrentVerifiedFactSnapshot(Guid snapshotId)
    {
        CurrentVerifiedFactSnapshotId = snapshotId;
    }

    public void UpdateCurrentVerifiedFactSnapshot(Guid snapshotId)
    {
        SetCurrentVerifiedFactSnapshot(snapshotId);
    }

    public void RecordScreeningResult(ScreeningResult screening)
    {
        if (screening == null)
        {
            throw new ArgumentNullException(nameof(screening), "ScreeningResult cannot be null.");
        }

        if (!screening.LeaseCaseId.Equals(Id))
        {
            throw new InvalidOperationException("Screening result LeaseCaseId does not match this lease case.");
        }

        LatestScreening = screening;
        _domainEvents.Add(new ScreeningResultRecorded(
            Guid.NewGuid(),
            screening.AssessedAtUtc,
            Id,
            screening.Id,
            screening.VerifiedFactSnapshotId,
            screening.Outcome));
    }

    private void EnsureScreeningGatePassed()
    {
        if (LatestScreening == null)
        {
            throw new MissingScreeningException("Screening result is missing for lease case.");
        }

        if (LatestScreening.IsStale(CurrentVerifiedFactSnapshotId))
        {
            throw new StaleScreeningException("Screening result is stale for the current verified fact snapshot.");
        }

        if (LatestScreening.Outcome == ScreeningOutcome.Blocked)
        {
            throw new BlockedScreeningException("Screening outcome is blocked.");
        }
    }

    public void ProgressWorkflow()
    {
        EnsureScreeningGatePassed();
    }

    public void StartWorkflow()
    {
        ProgressWorkflow();
    }

    public void ReviseWorkflowPlan(WorkflowPlan newPlan, VerifiedAuthoritySnapshot authority, string reason)
    {
        if (newPlan == null)
        {
            throw new ArgumentNullException(nameof(newPlan), "WorkflowPlan cannot be null.");
        }

        if (authority == null)
        {
            throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required to revise workflow plan.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidWorkflowPlanException("Revision reason is required.");
        }

        if (!newPlan.LeaseCaseId.Equals(Id))
        {
            throw new InvalidWorkflowPlanException("New workflow plan does not belong to this lease case.");
        }

        if (newPlan.VerifiedFactSnapshotId.Value != CurrentVerifiedFactSnapshotId)
        {
            throw new InvalidWorkflowPlanException("New workflow plan VerifiedFactSnapshotId does not match the current verified fact snapshot of the lease case.");
        }

        var actionTime = DateTime.UtcNow;
        if (actionTime < authority.ValidFrom || actionTime > authority.ValidUntil)
        {
            actionTime = authority.VerificationTime;
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Id.Value.ToString("D"));
        var capability = authority.Capabilities.Contains("WorkflowPlanSuperseder")
            ? "WorkflowPlanSuperseder"
            : "WorkflowPlanner";
        authority.EnsureAuthorizes(authority.ActorId, capability, requiredScope, actionTime);

        if (ActiveWorkflowPlan != null)
        {
            ActiveWorkflowPlan.SupersedePlan(authority.ActorId, reason, actionTime, authority);
        }

        ActiveWorkflowPlan = newPlan;
        _workflowPlanHistory.Add(newPlan);
        Revision++;
    }

    public void SetInitialWorkflowPlan(WorkflowPlan plan)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan), "WorkflowPlan cannot be null.");
        }

        ActiveWorkflowPlan = plan;
        if (!_workflowPlanHistory.Contains(plan))
        {
            _workflowPlanHistory.Add(plan);
        }
    }

    public void AttachInitialWorkflowPlan(WorkflowPlan plan)
    {
        SetInitialWorkflowPlan(plan);
    }

    public void AddApprovalCondition(ApprovalCondition condition)
    {
        if (condition == null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        if (!condition.LeaseCaseId.Equals(Id))
        {
            throw new InvalidFulfillmentException("ApprovalCondition does not belong to this lease case.");
        }

        if (_approvalConditions.Any(c => c.Id == condition.Id))
        {
            throw new InvalidFulfillmentException($"ApprovalCondition with ID '{condition.Id}' already exists.");
        }

        _approvalConditions.Add(condition);
    }

    public ApprovalCondition AddApprovalCondition(InstitutionCode institutionCode, string description, Guid? conditionId = null)
    {
        var condition = new ApprovalCondition(conditionId ?? Guid.NewGuid(), Id, institutionCode, description);
        AddApprovalCondition(condition);
        return condition;
    }

    public void AddDocumentSubmissionRequirement(DocumentSubmissionRequirement requirement)
    {
        if (requirement == null)
        {
            throw new ArgumentNullException(nameof(requirement));
        }

        if (!requirement.LeaseCaseId.Equals(Id))
        {
            throw new InvalidFulfillmentException("DocumentSubmissionRequirement does not belong to this lease case.");
        }

        if (_documentSubmissionRequirements.Any(r => r.Id == requirement.Id))
        {
            throw new InvalidFulfillmentException($"DocumentSubmissionRequirement with ID '{requirement.Id}' already exists.");
        }

        _documentSubmissionRequirements.Add(requirement);
    }

    public DocumentSubmissionRequirement AddDocumentSubmissionRequirement(DocumentClassificationCode classificationCode, DateTime dueDateUtc, Guid? requirementId = null)
    {
        var requirement = new DocumentSubmissionRequirement(requirementId ?? Guid.NewGuid(), Id, classificationCode, dueDateUtc);
        AddDocumentSubmissionRequirement(requirement);
        return requirement;
    }

    public void FulfillCondition(Guid conditionId, DateTime fulfilledAt)
    {
        var condition = _approvalConditions.FirstOrDefault(c => c.Id == conditionId);
        if (condition == null)
        {
            throw new InvalidFulfillmentException($"Approval condition with ID '{conditionId}' was not found.");
        }

        condition.Fulfill(fulfilledAt);
    }

    public void FulfillDocumentRequirement(Guid requirementId, GovernedDocumentId documentId, DateTime fulfilledAt)
    {
        var requirement = _documentSubmissionRequirements.FirstOrDefault(r => r.Id == requirementId);
        if (requirement == null)
        {
            throw new InvalidFulfillmentException($"Document submission requirement with ID '{requirementId}' was not found.");
        }

        requirement.FulfillWithDocument(documentId, fulfilledAt);
    }
}
