namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class ProposalContentCompletenessResult
{
    public ProposalContentAssessmentResultId Id { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public ProposalSourceBinding SourceBinding { get; }
    public ProposalTemplateSnapshot TemplateSnapshot { get; }
    public ProposalContentCompletenessOutcome Outcome { get; }

    private readonly ProposalContentRequirement[] _satisfiedMandatory;
    public IReadOnlyCollection<ProposalContentRequirement> SatisfiedMandatory => Array.AsReadOnly(_satisfiedMandatory);

    private readonly ProposalContentRequirement[] _missingMandatory;
    public IReadOnlyCollection<ProposalContentRequirement> MissingMandatory => Array.AsReadOnly(_missingMandatory);

    private readonly ProposalContentRequirement[] _reviewRequiredMandatory;
    public IReadOnlyCollection<ProposalContentRequirement> ReviewRequiredMandatory => Array.AsReadOnly(_reviewRequiredMandatory);

    private readonly ProposalContentRequirement[] _missingOptional;
    public IReadOnlyCollection<ProposalContentRequirement> MissingOptional => Array.AsReadOnly(_missingOptional);

    private readonly ProposalContentRequirement[] _satisfiedOptional;
    public IReadOnlyCollection<ProposalContentRequirement> SatisfiedOptional => Array.AsReadOnly(_satisfiedOptional);

    private readonly ProposalContentRequirement[] _unresolvedOptional;
    public IReadOnlyCollection<ProposalContentRequirement> UnresolvedOptional => Array.AsReadOnly(_unresolvedOptional);

    private readonly string[] _explanations;
    public IReadOnlyCollection<string> Explanations => Array.AsReadOnly(_explanations);

    public bool IsConfirmed { get; }
    public Guid? ConfirmedByActorId { get; }
    public DateTime? ConfirmedAtUtc { get; }
    public string? ConfirmationNotes { get; }

    public ProposalContentAssessmentResultId? ConfirmedResultOfId { get; }
    public ProposalContentAssessmentResultId? SupersedesResultId { get; }
    public string? CorrectionReason { get; }
    public string? CorrectionEvidenceReference { get; }
    public int SequenceNumber { get; }

    public bool CanSatisfyCompletenessGate => IsConfirmed && Outcome == ProposalContentCompletenessOutcome.Complete;

    public ProposalContentCompletenessResult(
        ProposalContentAssessmentResultId id,
        LeaseCaseId leaseCaseId,
        ProposalSourceBinding sourceBinding,
        ProposalTemplateSnapshot templateSnapshot,
        ProposalContentCompletenessOutcome outcome,
        IEnumerable<ProposalContentRequirement> satisfiedMandatory,
        IEnumerable<ProposalContentRequirement> missingMandatory,
        IEnumerable<ProposalContentRequirement> reviewRequiredMandatory,
        IEnumerable<ProposalContentRequirement> missingOptional,
        IEnumerable<ProposalContentRequirement> satisfiedOptional,
        IEnumerable<string> explanations,
        IEnumerable<ProposalContentRequirement>? unresolvedOptional = null,
        bool isConfirmed = false,
        Guid? confirmedByActorId = null,
        DateTime? confirmedAtUtc = null,
        string? confirmationNotes = null,
        ProposalContentAssessmentResultId? confirmedResultOfId = null,
        ProposalContentAssessmentResultId? supersedesResultId = null,
        string? correctionReason = null,
        string? correctionEvidenceReference = null,
        int sequenceNumber = 0)
    {
        if (id == default || id.Value == Guid.Empty)
        {
            throw new InvalidProposalContentAssessmentException("Result ID cannot be empty.");
        }

        if (leaseCaseId == default || leaseCaseId.Value == Guid.Empty)
        {
            throw new InvalidProposalContentAssessmentException("LeaseCaseId cannot be empty.");
        }

        if (sourceBinding == null)
        {
            throw new InvalidProposalContentAssessmentException("ProposalSourceBinding is required.");
        }

        if (!sourceBinding.LeaseCaseId.Equals(leaseCaseId))
        {
            throw new InvalidProposalContentAssessmentException("SourceBinding LeaseCaseId does not match Result LeaseCaseId.");
        }

        if (templateSnapshot == null)
        {
            throw new InvalidProposalContentAssessmentException("ProposalTemplateSnapshot is required.");
        }

        if (!sourceBinding.MatchesTemplate(templateSnapshot.TemplateId, templateSnapshot.TemplateVersion))
        {
            throw new InvalidProposalContentAssessmentException(
                "SourceBinding template identifier or version does not match TemplateSnapshot.");
        }

        if (!Enum.IsDefined(typeof(ProposalContentCompletenessOutcome), outcome))
        {
            throw new InvalidProposalContentAssessmentException($"Invalid ProposalContentCompletenessOutcome: {outcome}.");
        }

        if (satisfiedMandatory == null || missingMandatory == null || reviewRequiredMandatory == null ||
            missingOptional == null || satisfiedOptional == null || explanations == null)
        {
            throw new InvalidProposalContentAssessmentException("Requirement outcome collections and explanations cannot be null.");
        }

        _satisfiedMandatory = satisfiedMandatory.ToArray();
        _missingMandatory = missingMandatory.ToArray();
        _reviewRequiredMandatory = reviewRequiredMandatory.ToArray();
        _missingOptional = missingOptional.ToArray();
        _satisfiedOptional = satisfiedOptional.ToArray();
        _unresolvedOptional = unresolvedOptional?.ToArray() ?? Array.Empty<ProposalContentRequirement>();
        _explanations = explanations.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).ToArray();

        // Validate requirement membership against template snapshot
        var validReqIds = templateSnapshot.Requirements.Select(r => r.Id).ToHashSet();
        var allEvaluatedReqs = _satisfiedMandatory
            .Concat(_missingMandatory)
            .Concat(_reviewRequiredMandatory)
            .Concat(_missingOptional)
            .Concat(_satisfiedOptional)
            .Concat(_unresolvedOptional);

        foreach (var req in allEvaluatedReqs)
        {
            if (!validReqIds.Contains(req.Id))
            {
                throw new InvalidProposalContentAssessmentException(
                    $"Requirement '{req.Id}' is not present in template snapshot '{templateSnapshot.TemplateId}' version '{templateSnapshot.TemplateVersion}'.");
            }
        }

        // Validate outcome-specific consistency invariants
        switch (outcome)
        {
            case ProposalContentCompletenessOutcome.Complete:
                if (_missingMandatory.Length > 0)
                {
                    throw new InvalidProposalContentAssessmentException("A Complete outcome cannot contain missing mandatory requirements.");
                }
                if (_reviewRequiredMandatory.Length > 0)
                {
                    throw new InvalidProposalContentAssessmentException("A Complete outcome cannot contain review-required mandatory requirements.");
                }
                break;

            case ProposalContentCompletenessOutcome.Incomplete:
                if (_missingMandatory.Length == 0)
                {
                    throw new InvalidProposalContentAssessmentException("An Incomplete outcome must contain at least one missing mandatory requirement.");
                }
                break;

            case ProposalContentCompletenessOutcome.HumanReviewRequired:
                var isDraftOrAwaiting = templateSnapshot.Status == ProposalTemplateStatus.Draft ||
                                        templateSnapshot.Status == ProposalTemplateStatus.AwaitingConfirmation;
                if (_reviewRequiredMandatory.Length == 0 && !isDraftOrAwaiting)
                {
                    throw new InvalidProposalContentAssessmentException(
                        "A HumanReviewRequired outcome must have at least one mandatory review-required item or a draft/awaiting-confirmation template.");
                }
                break;
        }

        if (isConfirmed)
        {
            if (!confirmedByActorId.HasValue || confirmedByActorId.Value == Guid.Empty)
            {
                throw new InvalidProposalContentReviewException("Confirmed result must specify a valid ConfirmedByActorId.");
            }

            if (!confirmedAtUtc.HasValue || confirmedAtUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new InvalidProposalContentReviewException("Confirmed result must specify a valid UTC ConfirmedAtUtc timestamp.");
            }
        }

        if (supersedesResultId.HasValue)
        {
            if (supersedesResultId.Value.Value == Guid.Empty)
            {
                throw new InvalidProposalContentReviewException("SupersedesResultId cannot be empty when provided.");
            }

            if (string.IsNullOrWhiteSpace(correctionReason))
            {
                throw new InvalidProposalContentReviewException("CorrectionReason is required when superseding a prior result.");
            }

            if (string.IsNullOrWhiteSpace(correctionEvidenceReference))
            {
                throw new InvalidProposalContentReviewException("CorrectionEvidenceReference is required when superseding a prior result.");
            }
        }

        Id = id;
        LeaseCaseId = leaseCaseId;
        SourceBinding = sourceBinding;
        TemplateSnapshot = templateSnapshot;
        Outcome = outcome;

        IsConfirmed = isConfirmed;
        ConfirmedByActorId = confirmedByActorId;
        ConfirmedAtUtc = confirmedAtUtc;
        ConfirmationNotes = confirmationNotes?.Trim();
        ConfirmedResultOfId = confirmedResultOfId;
        SupersedesResultId = supersedesResultId;
        CorrectionReason = correctionReason?.Trim();
        CorrectionEvidenceReference = correctionEvidenceReference?.Trim();
        SequenceNumber = sequenceNumber;
    }

    public bool IsCurrentFor(
        ProposalSourceBinding currentBinding,
        string expectedTemplateDigest)
    {
        if (currentBinding == null)
        {
            throw new ArgumentNullException(nameof(currentBinding));
        }

        if (string.IsNullOrWhiteSpace(expectedTemplateDigest))
        {
            throw new InvalidProposalTemplateException("Expected template digest is mandatory and cannot be empty.");
        }

        return SourceBinding.MatchesSource(currentBinding) &&
               string.Equals(TemplateSnapshot.DefinitionDigest, expectedTemplateDigest.Trim(), StringComparison.Ordinal);
    }

    public bool IsCurrentFor(
        ProposalSourceBinding currentBinding,
        ProposalTemplateSnapshot currentTemplateSnapshot)
    {
        if (currentTemplateSnapshot == null)
        {
            throw new InvalidProposalTemplateException("Current ProposalTemplateSnapshot cannot be null.");
        }

        return IsCurrentFor(currentBinding, currentTemplateSnapshot.DefinitionDigest);
    }

    public bool IsCurrentFor(
        DocumentVersionId versionId,
        DocumentChecksum checksum,
        ProposalTemplateId templateId,
        string templateVersion,
        string expectedTemplateDigest)
    {
        if (string.IsNullOrWhiteSpace(expectedTemplateDigest))
        {
            throw new InvalidProposalTemplateException("Expected template digest is mandatory and cannot be empty.");
        }

        return SourceBinding.MatchesSource(versionId, checksum, templateId, templateVersion) &&
               string.Equals(TemplateSnapshot.DefinitionDigest, expectedTemplateDigest.Trim(), StringComparison.Ordinal);
    }

    public bool IsCurrentFor(
        DocumentVersionId versionId,
        DocumentChecksum checksum,
        ProposalTemplateId templateId,
        string templateVersion,
        ProposalTemplateSnapshot currentTemplateSnapshot)
    {
        if (currentTemplateSnapshot == null)
        {
            throw new InvalidProposalTemplateException("Current ProposalTemplateSnapshot cannot be null.");
        }

        return IsCurrentFor(versionId, checksum, templateId, templateVersion, currentTemplateSnapshot.DefinitionDigest);
    }

    public ProposalContentCompletenessResult WithConfirmation(
        ProposalContentAssessmentResultId confirmedResultId,
        Guid actorId,
        DateTime confirmedAtUtc,
        string? notes,
        int sequenceNumber)
    {
        if (confirmedResultId == default || confirmedResultId.Value == Guid.Empty)
        {
            throw new InvalidProposalContentReviewException("Confirmed result ID cannot be empty.");
        }

        if (actorId == Guid.Empty)
        {
            throw new InvalidProposalContentReviewException("ActorId cannot be empty.");
        }

        if (confirmedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new InvalidProposalContentReviewException("ConfirmedAtUtc must be UTC.");
        }

        return new ProposalContentCompletenessResult(
            confirmedResultId,
            LeaseCaseId,
            SourceBinding,
            TemplateSnapshot,
            Outcome,
            _satisfiedMandatory,
            _missingMandatory,
            _reviewRequiredMandatory,
            _missingOptional,
            _satisfiedOptional,
            _explanations,
            _unresolvedOptional,
            isConfirmed: true,
            confirmedByActorId: actorId,
            confirmedAtUtc: confirmedAtUtc,
            confirmationNotes: notes,
            confirmedResultOfId: Id,
            supersedesResultId: SupersedesResultId,
            correctionReason: CorrectionReason,
            correctionEvidenceReference: CorrectionEvidenceReference,
            sequenceNumber: sequenceNumber
        );
    }

    public ProposalContentCompletenessResult WithSequenceNumber(int sequenceNumber)
    {
        return new ProposalContentCompletenessResult(
            Id,
            LeaseCaseId,
            SourceBinding,
            TemplateSnapshot,
            Outcome,
            _satisfiedMandatory,
            _missingMandatory,
            _reviewRequiredMandatory,
            _missingOptional,
            _satisfiedOptional,
            _explanations,
            _unresolvedOptional,
            IsConfirmed,
            ConfirmedByActorId,
            ConfirmedAtUtc,
            ConfirmationNotes,
            ConfirmedResultOfId,
            SupersedesResultId,
            CorrectionReason,
            CorrectionEvidenceReference,
            sequenceNumber
        );
    }
}
