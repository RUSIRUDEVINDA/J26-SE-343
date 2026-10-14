namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

public static class DocumentCompletenessMapper
{
    public static DocumentCompletenessAssessmentDto ToDto(DocumentCompletenessAssessment assessment)
    {
        if (assessment == null)
        {
            throw new ArgumentNullException(nameof(assessment));
        }

        var assessedDocs = assessment.AssessedDocuments
            .Select(ToDto)
            .ToList();

        var classifiedDocs = assessment.ClassifiedDocuments
            .Select(ToDto)
            .ToList();

        var reqs = assessment.Requirements
            .Select(ToDto)
            .ToList();

        var reviews = assessment.Reviews
            .Select(ToDto)
            .ToList();

        var missing = assessment.MissingRequirements
            .Select(ToDto)
            .ToList();

        return new DocumentCompletenessAssessmentDto(
            Id: assessment.Id.Value,
            LeaseCaseId: assessment.LeaseCaseId.Value,
            RequirementSetIdentifier: assessment.RequirementSetIdentifier,
            RequirementSetVersion: assessment.RequirementSetVersion,
            AssessedAt: assessment.AssessedAt,
            Outcome: assessment.Outcome.ToString(),
            Revision: assessment.Revision,
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs,
            Requirements: reqs,
            Reviews: reviews,
            MissingRequirements: missing
        );
    }

    public static AssessedDocumentBindingDto ToDto(AssessedDocumentBinding binding)
    {
        if (binding == null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        return new AssessedDocumentBindingDto(
            GovernedDocumentId: binding.GovernedDocumentId.Value,
            DocumentVersionId: binding.DocumentVersionId.Value,
            ChecksumAlgorithm: binding.DocumentChecksum.Algorithm,
            ChecksumValue: binding.DocumentChecksum.Value
        );
    }

    public static ClassifiedDocumentDto ToDto(ClassifiedDocument doc)
    {
        if (doc == null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

        return new ClassifiedDocumentDto(
            Id: doc.Id.Value,
            DocumentBinding: ToDto(doc.DocumentBinding),
            OriginalClassificationCode: doc.OriginalClassificationCode?.Value,
            ConfidenceScore: doc.Confidence?.Value,
            Status: doc.Status.ToString(),
            ClassifiedAt: doc.ClassifiedAt
        );
    }

    public static DocumentRequirementSnapshotDto ToDto(DocumentRequirementSnapshot req)
    {
        if (req == null)
        {
            throw new ArgumentNullException(nameof(req));
        }

        return new DocumentRequirementSnapshotDto(
            Id: req.Id.Value,
            RequiredClassificationCode: req.RequiredClassificationCode.Value,
            MinimumRequiredCount: req.MinimumRequiredCount,
            Applicability: req.Applicability.ToString(),
            Criticality: req.Criticality.ToString(),
            ReasonCode: req.ReasonCode,
            Description: req.Description
        );
    }

    public static ClassificationReviewDto ToDto(ClassificationReview review)
    {
        if (review == null)
        {
            throw new ArgumentNullException(nameof(review));
        }

        return new ClassificationReviewDto(
            Id: review.Id.Value,
            ClassifiedDocumentId: review.ClassifiedDocumentId.Value,
            OriginalClassificationCode: null,
            Decision: review.Decision.ToString(),
            CorrectedClassificationCode: review.CorrectedClassificationCode?.Value,
            Reason: review.Reason,
            ReviewingActorId: review.ReviewingActorId,
            ReviewedAt: review.ReviewedAt
        );
    }

    public static MissingRequiredDocumentDto ToDto(MissingRequiredDocument missing)
    {
        if (missing == null)
        {
            throw new ArgumentNullException(nameof(missing));
        }

        return new MissingRequiredDocumentDto(
            RequirementId: missing.RequirementId.Value,
            RequiredClassificationCode: missing.RequiredClassificationCode.Value,
            RequiredCount: missing.RequiredCount,
            SatisfiedCount: missing.SatisfiedCount,
            Description: missing.Description
        );
    }
}
