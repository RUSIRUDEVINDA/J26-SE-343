namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentCompletenessTests;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using Xunit;

public class DocumentCompletenessAssessmentCoverageTests
{
    private static AssessedDocumentBinding CreateBinding(Guid docId) => new(
        new GovernedDocumentId(docId),
        new DocumentVersionId(Guid.NewGuid()),
        new DocumentChecksum("SHA256", "checksum")
    );

    [Fact]
    public void Optional_Requirement_DoesNotPreventComplete()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var classification = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, new DocumentClassificationCode("NOT-MATCH"), new ConfidenceScore(0.9m), DocumentClassificationStatus.Accepted, null, null, null, DateTime.UtcNow);
        var req = new DocumentRequirementSnapshot(new DocumentRequirementId(Guid.NewGuid()), new DocumentClassificationCode("OPTIONAL"), RequirementCriticality.Optional, RequirementApplicability.Required, 1, "C", "D");
        
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { classification }, new[] { req }, DateTime.UtcNow);
        Assert.Equal(CompletenessAssessmentOutcome.Complete, sut.Outcome);
    }

    [Fact]
    public void MinimumRequiredCount_CountsDistinctMatchingDocuments()
    {
        var binding1 = CreateBinding(Guid.NewGuid());
        var binding2 = CreateBinding(Guid.NewGuid());
        var doc1 = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding1, new DocumentClassificationCode("MATCH"), new ConfidenceScore(0.9m), DocumentClassificationStatus.Accepted, null, null, null, DateTime.UtcNow);
        var doc2 = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding2, new DocumentClassificationCode("MATCH"), new ConfidenceScore(0.9m), DocumentClassificationStatus.Accepted, null, null, null, DateTime.UtcNow);
        
        var req = new DocumentRequirementSnapshot(new DocumentRequirementId(Guid.NewGuid()), new DocumentClassificationCode("MATCH"), RequirementCriticality.Mandatory, RequirementApplicability.Required, 2, "C", "D");
        
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding1, binding2 }, new[] { doc1, doc2 }, new[] { req }, DateTime.UtcNow);
        Assert.Equal(CompletenessAssessmentOutcome.Complete, sut.Outcome);
    }

    [Fact]
    public void Confirmed_Review_Of_Unclassified_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        
        Assert.Throws<InvalidClassificationReviewException>(() => new ClassificationReview(
            new ClassificationReviewId(Guid.NewGuid()), doc.Id, doc.OriginalClassificationCode, ClassificationReviewDecision.Confirmed, null, null, Guid.NewGuid(), DateTime.UtcNow, "Cap", AuthorityScopeKind.GovernedDocument, "T", AuthorityScopeKind.GovernedDocument, "T", DateTime.UtcNow));
    }

    [Fact]
    public void Review_DuplicateId_ThrowsDuplicateOrConflicting()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { doc }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        var reviewId = new ClassificationReviewId(Guid.NewGuid());
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(actorId, new[] { "ClassificationReviewer" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

        sut.RecordHumanClassificationReview(reviewId, doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("R"), "R", actorId, DateTime.UtcNow, authority);

        // Identical
        Assert.Throws<DuplicateClassificationReviewException>(() => sut.RecordHumanClassificationReview(reviewId, doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("R"), "R", actorId, DateTime.UtcNow, authority));
        
        // Conflicting
        Assert.Throws<ConflictingClassificationReviewException>(() => sut.RecordHumanClassificationReview(reviewId, doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("R"), "R2", actorId, DateTime.UtcNow, authority));
    }

    [Fact]
    public void Review_AlreadyReviewed_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { doc }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(actorId, new[] { "ClassificationReviewer" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

        sut.RecordHumanClassificationReview(new ClassificationReviewId(Guid.NewGuid()), doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("R"), "R", actorId, DateTime.UtcNow, authority);
        
        Assert.Throws<ClassificationAlreadyReviewedException>(() => sut.RecordHumanClassificationReview(new ClassificationReviewId(Guid.NewGuid()), doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("R"), "R", actorId, DateTime.UtcNow, authority));
    }

    [Fact]
    public void Review_WrongScope_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { doc }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        var actorId = Guid.NewGuid();
        // Wrong target
        var authority = new VerifiedAuthoritySnapshot(actorId, new[] { "ClassificationReviewer" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, Guid.NewGuid().ToString("D")), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

        Assert.Throws<MissingVerifiedAuthorityException>(() => sut.RecordHumanClassificationReview(new ClassificationReviewId(Guid.NewGuid()), doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("R"), "R", actorId, DateTime.UtcNow, authority));
    }
}
