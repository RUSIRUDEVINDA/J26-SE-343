namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ClassifiedDocument
{
    public ClassifiedDocumentId Id { get; }
    public AssessedDocumentBinding DocumentBinding { get; }
    public DocumentClassificationCode? OriginalClassificationCode { get; }
    public ConfidenceScore? Confidence { get; }
    public DocumentClassificationStatus Status { get; }
    public AnalysisRunId? SourceAnalysisRunId { get; }
    public AnalysisRunResultId? SourceAnalysisRunResultId { get; }
    public AnalysisModelReference? ModelReference { get; }
    public DateTime ClassifiedAt { get; }

    public ClassifiedDocument(
        ClassifiedDocumentId id,
        AssessedDocumentBinding documentBinding,
        DocumentClassificationCode? originalClassificationCode,
        ConfidenceScore? confidence,
        DocumentClassificationStatus status,
        AnalysisRunId? sourceAnalysisRunId,
        AnalysisRunResultId? sourceAnalysisRunResultId,
        AnalysisModelReference? modelReference,
        DateTime classifiedAt)
    {
        if (id.Value == Guid.Empty) throw new InvalidDocumentCompletenessAssessmentException("Id cannot be empty.");
        if (documentBinding == null) throw new InvalidDocumentCompletenessAssessmentException("DocumentBinding is required.");
        if (classifiedAt.Kind != DateTimeKind.Utc) throw new InvalidDocumentCompletenessAssessmentException("ClassifiedAt must be UTC.");

        if (status == DocumentClassificationStatus.Accepted)
        {
            if (originalClassificationCode == null || confidence == null)
                throw new InvalidDocumentCompletenessAssessmentException("Accepted requires code and confidence.");
        }
        else if (status == DocumentClassificationStatus.Uncertain)
        {
            if (originalClassificationCode == null || confidence == null)
                throw new InvalidDocumentCompletenessAssessmentException("Uncertain requires code and confidence.");
        }
        else if (status == DocumentClassificationStatus.Unclassified)
        {
            if (originalClassificationCode != null || confidence != null)
                throw new InvalidDocumentCompletenessAssessmentException("Unclassified requires null code and confidence.");
        }

        Id = id;
        DocumentBinding = documentBinding;
        OriginalClassificationCode = originalClassificationCode;
        Confidence = confidence;
        Status = status;
        SourceAnalysisRunId = sourceAnalysisRunId;
        SourceAnalysisRunResultId = sourceAnalysisRunResultId;
        ModelReference = modelReference;
        ClassifiedAt = classifiedAt;
    }
}
