namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public static class ProposalContentMapper
{
    public static ProposalContentAssessmentDto ToDto(ProposalContentCompletenessResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var sourceBinding = ToDto(result.SourceBinding);
        var templateSnapshot = ToDto(result.TemplateSnapshot);

        var satisfiedMandatory = result.SatisfiedMandatory
            .Select(ToDto)
            .ToList();

        var missingMandatory = result.MissingMandatory
            .Select(ToDto)
            .ToList();

        var reviewRequired = result.ReviewRequiredMandatory
            .Select(ToDto)
            .ToList();

        var missingOptional = result.MissingOptional
            .Select(ToDto)
            .ToList();

        var satisfiedOptional = result.SatisfiedOptional
            .Select(ToDto)
            .ToList();

        var explanations = result.Explanations.ToList();

        var unresolvedOptional = result.UnresolvedOptional
            .Select(ToDto)
            .ToList();

        return new ProposalContentAssessmentDto(
            Id: result.Id.Value,
            LeaseCaseId: result.LeaseCaseId.Value,
            SourceBinding: sourceBinding,
            TemplateSnapshot: templateSnapshot,
            Outcome: result.Outcome.ToString(),
            SatisfiedMandatory: satisfiedMandatory,
            MissingMandatory: missingMandatory,
            ReviewRequiredMandatory: reviewRequired,
            MissingOptional: missingOptional,
            SatisfiedOptional: satisfiedOptional,
            Explanations: explanations,
            UnresolvedOptional: unresolvedOptional,
            IsConfirmed: result.IsConfirmed,
            ConfirmedByActorId: result.ConfirmedByActorId,
            ConfirmedAtUtc: result.ConfirmedAtUtc,
            ConfirmationNotes: result.ConfirmationNotes,
            ConfirmedResultOfId: result.ConfirmedResultOfId?.Value,
            SupersedesResultId: result.SupersedesResultId?.Value,
            CorrectionReason: result.CorrectionReason,
            CorrectionEvidenceReference: result.CorrectionEvidenceReference,
            SequenceNumber: result.SequenceNumber
        );
    }

    public static ProposalSourceBindingDto ToDto(ProposalSourceBinding binding)
    {
        if (binding == null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        return new ProposalSourceBindingDto(
            LeaseCaseId: binding.LeaseCaseId.Value,
            ProposalDocumentId: binding.ProposalDocumentId.Value,
            DocumentVersionId: binding.DocumentVersionId.Value,
            ChecksumAlgorithm: binding.DocumentChecksum.Algorithm,
            ChecksumValue: binding.DocumentChecksum.Value,
            TemplateId: binding.TemplateId.Value,
            TemplateVersion: binding.TemplateVersion,
            AssessedAt: binding.AssessedAt,
            ExtractionReference: binding.ExtractionReference
        );
    }

    public static ProposalTemplateSnapshotDto ToDto(ProposalTemplateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var reqs = snapshot.Requirements
            .Select(ToDto)
            .ToList();

        return new ProposalTemplateSnapshotDto(
            TemplateId: snapshot.TemplateId.Value,
            TemplateVersion: snapshot.TemplateVersion,
            Name: snapshot.Name,
            AuthoritativeSourceReference: snapshot.AuthoritativeSourceReference,
            Status: snapshot.Status.ToString(),
            DefinitionDigest: snapshot.DefinitionDigest,
            Requirements: reqs
        );
    }

    public static ProposalContentRequirementDto ToDto(ProposalContentRequirement req)
    {
        if (req == null)
        {
            throw new ArgumentNullException(nameof(req));
        }

        return new ProposalContentRequirementDto(
            Id: req.Id.Value,
            Kind: req.Kind.ToString(),
            DisplayName: req.DisplayName,
            IsMandatory: req.IsMandatory,
            TemplateId: req.TemplateId.Value,
            TemplateVersion: req.TemplateVersion,
            AuthoritativeSourceReference: req.AuthoritativeSourceReference,
            OrderIndex: req.OrderIndex,
            ApplicabilityContext: req.ApplicabilityContext
        );
    }
}
