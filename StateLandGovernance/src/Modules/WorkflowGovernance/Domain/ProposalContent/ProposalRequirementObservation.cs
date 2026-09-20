namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record ProposalRequirementObservation
{
    public ProposalRequirementId RequirementId { get; }
    public ProposalObservationState State { get; }
    public ProposalSourceBinding SourceBinding { get; }
    public int? PageNumber { get; }
    public string? TextSpan { get; }
    public string? EvidenceReference { get; }
    public string? ExtractionReference { get; }
    public string? Explanation { get; }
    public bool IsMachineGenerated { get; }

    public ProposalRequirementObservation(
        ProposalRequirementId requirementId,
        ProposalObservationState state,
        ProposalSourceBinding sourceBinding,
        int? pageNumber = null,
        string? textSpan = null,
        string? evidenceReference = null,
        string? extractionReference = null,
        string? explanation = null,
        bool isMachineGenerated = true)
    {
        if (requirementId == default || string.IsNullOrWhiteSpace(requirementId.Value))
        {
            throw new InvalidProposalObservationException("RequirementId is required.");
        }

        if (!Enum.IsDefined(typeof(ProposalObservationState), state))
        {
            throw new InvalidProposalObservationException($"Invalid ProposalObservationState: {state}.");
        }

        if (sourceBinding == null)
        {
            throw new InvalidProposalObservationException("ProposalSourceBinding is required for all observations.");
        }

        if (pageNumber.HasValue && pageNumber.Value <= 0)
        {
            throw new InvalidProposalObservationException($"PageNumber must be positive. Received: {pageNumber.Value}.");
        }

        if (textSpan != null && string.IsNullOrWhiteSpace(textSpan))
        {
            throw new InvalidProposalObservationException("TextSpan cannot be empty or whitespace when supplied.");
        }

        if (evidenceReference != null && string.IsNullOrWhiteSpace(evidenceReference))
        {
            throw new InvalidProposalObservationException("EvidenceReference cannot be empty or whitespace when supplied.");
        }

        if (extractionReference != null && string.IsNullOrWhiteSpace(extractionReference))
        {
            throw new InvalidProposalObservationException("ExtractionReference cannot be empty or whitespace when supplied.");
        }

        if (state == ProposalObservationState.Present)
        {
            var hasEvidence = !string.IsNullOrWhiteSpace(textSpan) || !string.IsNullOrWhiteSpace(evidenceReference);
            if (!hasEvidence)
            {
                throw new InvalidProposalObservationException(
                    "A Present observation must retain a non-empty evidence reference or text span sufficient for officer review.");
            }
        }
        else
        {
            var effectiveReason = !string.IsNullOrWhiteSpace(explanation) ? explanation : evidenceReference;
            if (string.IsNullOrWhiteSpace(effectiveReason))
            {
                throw new InvalidProposalObservationException(
                    $"Observation with state '{state}' must contain a non-empty explanation or reason.");
            }
        }

        RequirementId = requirementId;
        State = state;
        SourceBinding = sourceBinding;
        PageNumber = pageNumber;
        TextSpan = textSpan?.Trim();
        EvidenceReference = evidenceReference?.Trim();
        ExtractionReference = extractionReference?.Trim();
        Explanation = explanation?.Trim() ?? (!string.IsNullOrWhiteSpace(evidenceReference) && state != ProposalObservationState.Present ? evidenceReference.Trim() : null);
        IsMachineGenerated = isMachineGenerated;
    }
}
