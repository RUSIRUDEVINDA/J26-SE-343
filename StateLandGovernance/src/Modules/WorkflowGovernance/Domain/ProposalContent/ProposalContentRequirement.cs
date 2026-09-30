namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record ProposalContentRequirement
{
    public ProposalRequirementId Id { get; }
    public ProposalRequirementKind Kind { get; }
    public string DisplayName { get; }
    public bool IsMandatory { get; }
    public ProposalTemplateId TemplateId { get; }
    public string TemplateVersion { get; }
    public string AuthoritativeSourceReference { get; }
    public int OrderIndex { get; }
    public string? ApplicabilityContext { get; }

    public ProposalContentRequirement(
        ProposalRequirementId id,
        ProposalRequirementKind kind,
        string displayName,
        bool isMandatory,
        ProposalTemplateId templateId,
        string templateVersion,
        string authoritativeSourceReference,
        int orderIndex,
        string? applicabilityContext = null)
    {
        if (id == default || string.IsNullOrWhiteSpace(id.Value))
        {
            throw new InvalidProposalContentRequirementException("Requirement identifier is required.");
        }

        if (!Enum.IsDefined(typeof(ProposalRequirementKind), kind))
        {
            throw new InvalidProposalContentRequirementException($"Invalid ProposalRequirementKind: {kind}.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidProposalContentRequirementException("DisplayName cannot be null, empty, or whitespace.");
        }

        if (templateId == default || string.IsNullOrWhiteSpace(templateId.Value))
        {
            throw new InvalidProposalContentRequirementException("TemplateId is required.");
        }

        if (string.IsNullOrWhiteSpace(templateVersion))
        {
            throw new InvalidProposalContentRequirementException("TemplateVersion cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(authoritativeSourceReference))
        {
            throw new InvalidProposalContentRequirementException("AuthoritativeSourceReference cannot be null, empty, or whitespace.");
        }

        if (orderIndex <= 0)
        {
            throw new InvalidProposalContentRequirementException($"OrderIndex must be positive. Received: {orderIndex}.");
        }

        if (applicabilityContext != null && string.IsNullOrWhiteSpace(applicabilityContext))
        {
            throw new InvalidProposalContentRequirementException("ApplicabilityContext cannot be whitespace when provided.");
        }

        Id = id;
        Kind = kind;
        DisplayName = displayName.Trim();
        IsMandatory = isMandatory;
        TemplateId = templateId;
        TemplateVersion = templateVersion.Trim();
        AuthoritativeSourceReference = authoritativeSourceReference.Trim();
        OrderIndex = orderIndex;
        ApplicabilityContext = applicabilityContext?.Trim();
    }
}
