namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

internal static class DocumentCompletenessAssessmentPersistenceMapper
{
    public static DocumentCompletenessAssessmentEntity ToPersistence(DocumentCompletenessAssessment domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var assessedDocsDtos = domain.AssessedDocuments.Select(b => new AssessedDocumentBindingDto
        {
            GovernedDocumentId = b.GovernedDocumentId.Value,
            DocumentVersionId = b.DocumentVersionId.Value,
            ChecksumAlgorithm = b.DocumentChecksum.Algorithm,
            ChecksumValue = b.DocumentChecksum.Value
        }).ToList();

        var classifiedDocsDtos = domain.ClassifiedDocuments.Select(c => new ClassifiedDocumentDto
        {
            Id = c.Id.Value,
            DocumentBinding = new AssessedDocumentBindingDto
            {
                GovernedDocumentId = c.DocumentBinding.GovernedDocumentId.Value,
                DocumentVersionId = c.DocumentBinding.DocumentVersionId.Value,
                ChecksumAlgorithm = c.DocumentBinding.DocumentChecksum.Algorithm,
                ChecksumValue = c.DocumentBinding.DocumentChecksum.Value
            },
            OriginalClassificationCode = c.OriginalClassificationCode?.Value,
            Confidence = c.Confidence?.Value,
            Status = c.Status.ToString(),
            SourceAnalysisRunId = c.SourceAnalysisRunId?.Value,
            SourceAnalysisRunResultId = c.SourceAnalysisRunResultId?.Value,
            ModelProvider = c.ModelReference?.Provider,
            ModelName = c.ModelReference?.ModelName,
            ModelVersion = c.ModelReference?.ModelVersion,
            ClassifiedAt = c.ClassifiedAt.ToUniversalTime()
        }).ToList();

        var requirementsDtos = domain.Requirements.Select(r => new DocumentRequirementSnapshotDto
        {
            Id = r.Id.Value,
            RequiredClassificationCode = r.RequiredClassificationCode.Value,
            Criticality = r.Criticality.ToString(),
            Applicability = r.Applicability.ToString(),
            MinimumRequiredCount = r.MinimumRequiredCount,
            ReasonCode = r.ReasonCode,
            Description = r.Description
        }).ToList();

        var reviewsDtos = domain.Reviews.Select(rv => new ClassificationReviewDto
        {
            Id = rv.Id.Value,
            ClassifiedDocumentId = rv.ClassifiedDocumentId.Value,
            Decision = rv.Decision.ToString(),
            CorrectedClassificationCode = rv.CorrectedClassificationCode?.Value,
            Reason = rv.Reason,
            ReviewingActorId = rv.ReviewingActorId,
            ReviewedAt = rv.ReviewedAt.ToUniversalTime(),
            VerifiedCapability = rv.VerifiedCapability,
            GrantedAuthorityScopeKind = rv.GrantedAuthorityScopeKind.ToString(),
            GrantedAuthorityScopeIdentifier = rv.GrantedAuthorityScopeIdentifier,
            RequiredAuthorityScopeKind = rv.RequiredAuthorityScopeKind.ToString(),
            RequiredAuthorityScopeIdentifier = rv.RequiredAuthorityScopeIdentifier,
            AuthorityVerificationTime = rv.AuthorityVerificationTime.ToUniversalTime()
        }).ToList();

        var missingDtos = domain.MissingRequirements.Select(m => new MissingRequiredDocumentDto
        {
            RequirementId = m.RequirementId.Value,
            RequiredClassificationCode = m.RequiredClassificationCode.Value,
            RequiredCount = m.RequiredCount,
            SatisfiedCount = m.SatisfiedCount,
            Description = m.Description
        }).ToList();

        return new DocumentCompletenessAssessmentEntity
        {
            Id = domain.Id.Value,
            LeaseCaseId = domain.LeaseCaseId.Value,
            RequirementSetIdentifier = domain.RequirementSetIdentifier,
            RequirementSetVersion = domain.RequirementSetVersion,
            AssessedAt = domain.AssessedAt.ToUniversalTime(),
            Outcome = domain.Outcome.ToString(),
            Revision = domain.Revision,
            AssessedDocumentsJson = PersistenceJsonSerializer.Serialize(assessedDocsDtos),
            ClassifiedDocumentsJson = PersistenceJsonSerializer.Serialize(classifiedDocsDtos),
            RequirementsJson = PersistenceJsonSerializer.Serialize(requirementsDtos),
            ReviewsJson = PersistenceJsonSerializer.Serialize(reviewsDtos),
            MissingRequirementsJson = PersistenceJsonSerializer.Serialize(missingDtos),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static DocumentCompletenessAssessment ToDomain(DocumentCompletenessAssessmentEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var domain = AggregateRehydrator.Create<DocumentCompletenessAssessment>();

        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentCompletenessAssessment.Id), new DocumentCompletenessAssessmentId(entity.Id));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentCompletenessAssessment.LeaseCaseId), new LeaseCaseId(entity.LeaseCaseId));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentCompletenessAssessment.RequirementSetIdentifier), entity.RequirementSetIdentifier);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentCompletenessAssessment.RequirementSetVersion), entity.RequirementSetVersion);
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentCompletenessAssessment.AssessedAt), DateTime.SpecifyKind(entity.AssessedAt, DateTimeKind.Utc));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentCompletenessAssessment.Outcome), Enum.Parse<CompletenessAssessmentOutcome>(entity.Outcome));
        AggregateRehydrator.SetPropertyBackingField(domain, nameof(DocumentCompletenessAssessment.Revision), entity.Revision);

        var assessedDtos = PersistenceJsonSerializer.Deserialize<List<AssessedDocumentBindingDto>>(entity.AssessedDocumentsJson) ?? new List<AssessedDocumentBindingDto>();
        var assessedDocs = assessedDtos.Select(b => new AssessedDocumentBinding(
            new GovernedDocumentId(b.GovernedDocumentId),
            new DocumentVersionId(b.DocumentVersionId),
            new DocumentChecksum(b.ChecksumAlgorithm, b.ChecksumValue)
        )).ToList();
        AggregateRehydrator.SetField(domain, "_assessedDocuments", assessedDocs);

        var classifiedDtos = PersistenceJsonSerializer.Deserialize<List<ClassifiedDocumentDto>>(entity.ClassifiedDocumentsJson) ?? new List<ClassifiedDocumentDto>();
        var classifiedDocs = classifiedDtos.Select(c =>
        {
            var binding = new AssessedDocumentBinding(
                new GovernedDocumentId(c.DocumentBinding.GovernedDocumentId),
                new DocumentVersionId(c.DocumentBinding.DocumentVersionId),
                new DocumentChecksum(c.DocumentBinding.ChecksumAlgorithm, c.DocumentBinding.ChecksumValue));

            return new ClassifiedDocument(
                new ClassifiedDocumentId(c.Id),
                binding,
                c.OriginalClassificationCode is null ? null : new DocumentClassificationCode(c.OriginalClassificationCode),
                c.Confidence.HasValue ? new ConfidenceScore(c.Confidence.Value) : null,
                Enum.Parse<DocumentClassificationStatus>(c.Status),
                c.SourceAnalysisRunId.HasValue ? new AnalysisRunId(c.SourceAnalysisRunId.Value) : null,
                c.SourceAnalysisRunResultId.HasValue ? new AnalysisRunResultId(c.SourceAnalysisRunResultId.Value) : null,
                c.ModelProvider is not null && c.ModelName is not null && c.ModelVersion is not null
                    ? new AnalysisModelReference(c.ModelProvider, c.ModelName, c.ModelVersion)
                    : null,
                DateTime.SpecifyKind(c.ClassifiedAt, DateTimeKind.Utc));
        }).ToList();
        AggregateRehydrator.SetField(domain, "_classifiedDocuments", classifiedDocs);

        var reqDtos = PersistenceJsonSerializer.Deserialize<List<DocumentRequirementSnapshotDto>>(entity.RequirementsJson) ?? new List<DocumentRequirementSnapshotDto>();
        var requirements = reqDtos.Select(r => new DocumentRequirementSnapshot(
            new DocumentRequirementId(r.Id),
            new DocumentClassificationCode(r.RequiredClassificationCode),
            Enum.Parse<RequirementCriticality>(r.Criticality),
            Enum.Parse<RequirementApplicability>(r.Applicability),
            r.MinimumRequiredCount,
            r.ReasonCode,
            r.Description
        )).ToList();
        AggregateRehydrator.SetField(domain, "_requirements", requirements);

        var reviewDtos = PersistenceJsonSerializer.Deserialize<List<ClassificationReviewDto>>(entity.ReviewsJson) ?? new List<ClassificationReviewDto>();
        var reviews = reviewDtos.Select(rv => new ClassificationReview(
            new ClassificationReviewId(rv.Id),
            new ClassifiedDocumentId(rv.ClassifiedDocumentId),
            null, // original classification is checked in domain method, ctor accepts it or we can pass null
            Enum.Parse<ClassificationReviewDecision>(rv.Decision),
            rv.CorrectedClassificationCode is null ? null : new DocumentClassificationCode(rv.CorrectedClassificationCode),
            rv.Reason,
            rv.ReviewingActorId,
            DateTime.SpecifyKind(rv.ReviewedAt, DateTimeKind.Utc),
            rv.VerifiedCapability,
            Enum.Parse<AuthorityScopeKind>(rv.GrantedAuthorityScopeKind),
            rv.GrantedAuthorityScopeIdentifier,
            Enum.Parse<AuthorityScopeKind>(rv.RequiredAuthorityScopeKind),
            rv.RequiredAuthorityScopeIdentifier,
            DateTime.SpecifyKind(rv.AuthorityVerificationTime, DateTimeKind.Utc)
        )).ToList();
        AggregateRehydrator.SetField(domain, "_reviews", reviews);

        var missingDtos = PersistenceJsonSerializer.Deserialize<List<MissingRequiredDocumentDto>>(entity.MissingRequirementsJson) ?? new List<MissingRequiredDocumentDto>();
        var missingRequirements = missingDtos.Select(m => new MissingRequiredDocument(
            new DocumentRequirementId(m.RequirementId),
            new DocumentClassificationCode(m.RequiredClassificationCode),
            m.RequiredCount,
            m.SatisfiedCount,
            m.Description
        )).ToList();
        AggregateRehydrator.SetField(domain, "_missingRequirements", missingRequirements);

        AggregateRehydrator.SetField(domain, "_domainEvents", new List<IDomainEvent>());

        return domain;
    }

    private sealed class AssessedDocumentBindingDto
    {
        public Guid GovernedDocumentId { get; set; }
        public Guid DocumentVersionId { get; set; }
        public string ChecksumAlgorithm { get; set; } = string.Empty;
        public string ChecksumValue { get; set; } = string.Empty;
    }

    private sealed class ClassifiedDocumentDto
    {
        public Guid Id { get; set; }
        public AssessedDocumentBindingDto DocumentBinding { get; set; } = new();
        public string? OriginalClassificationCode { get; set; }
        public decimal? Confidence { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? SourceAnalysisRunId { get; set; }
        public Guid? SourceAnalysisRunResultId { get; set; }
        public string? ModelProvider { get; set; }
        public string? ModelName { get; set; }
        public string? ModelVersion { get; set; }
        public DateTime ClassifiedAt { get; set; }
    }

    private sealed class DocumentRequirementSnapshotDto
    {
        public Guid Id { get; set; }
        public string RequiredClassificationCode { get; set; } = string.Empty;
        public string Criticality { get; set; } = string.Empty;
        public string Applicability { get; set; } = string.Empty;
        public int MinimumRequiredCount { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private sealed class ClassificationReviewDto
    {
        public Guid Id { get; set; }
        public Guid ClassifiedDocumentId { get; set; }
        public string Decision { get; set; } = string.Empty;
        public string? CorrectedClassificationCode { get; set; }
        public string? Reason { get; set; }
        public Guid ReviewingActorId { get; set; }
        public DateTime ReviewedAt { get; set; }
        public string VerifiedCapability { get; set; } = string.Empty;
        public string GrantedAuthorityScopeKind { get; set; } = string.Empty;
        public string? GrantedAuthorityScopeIdentifier { get; set; }
        public string RequiredAuthorityScopeKind { get; set; } = string.Empty;
        public string RequiredAuthorityScopeIdentifier { get; set; } = string.Empty;
        public DateTime AuthorityVerificationTime { get; set; }
    }

    private sealed class MissingRequiredDocumentDto
    {
        public Guid RequirementId { get; set; }
        public string RequiredClassificationCode { get; set; } = string.Empty;
        public int RequiredCount { get; set; }
        public int SatisfiedCount { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}

