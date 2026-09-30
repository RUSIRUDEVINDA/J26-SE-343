namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentCompletenessTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using Xunit;

public class DocumentCompletenessAssessmentMissingCoverageTests
{
    private static AssessedDocumentBinding CreateBinding(Guid docId) => new(
        new GovernedDocumentId(docId),
        new DocumentVersionId(Guid.NewGuid()),
        new DocumentChecksum("SHA256", "checksum")
    );

    [Fact]
    public void MissingClassificationForAssessedBinding_Throws()
    {
        var binding1 = CreateBinding(Guid.NewGuid());
        var binding2 = CreateBinding(Guid.NewGuid());
        
        var classification1 = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding1, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);

        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", 
            new[] { binding1, binding2 }, new[] { classification1 }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow));
    }

    [Fact]
    public void OriginalMLClassification_PreservedAfterCorrection()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var originalCode = new DocumentClassificationCode("ML-CODE");
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, originalCode, new ConfidenceScore(0.5m), DocumentClassificationStatus.Uncertain, null, null, null, DateTime.UtcNow);
        
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { doc }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        var actorId = Guid.NewGuid();
        var reviewedAt = DateTime.UtcNow;
        var authority = new VerifiedAuthoritySnapshot(actorId, new[] { "ClassificationReviewer" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

        sut.RecordHumanClassificationReview(new ClassificationReviewId(Guid.NewGuid()), doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("HUMAN"), "Re-classified", actorId, reviewedAt, authority);
        
        var storedDoc = sut.ClassifiedDocuments.Single();
        Assert.Equal(originalCode, storedDoc.OriginalClassificationCode);
    }

    [Fact]
    public void RevisionOverflow_Atomicity_PreservesState()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { doc }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        var revField = typeof(DocumentCompletenessAssessment).GetProperty("Revision", BindingFlags.Public | BindingFlags.Instance);
        revField!.SetValue(sut, int.MaxValue);

        var actorId = Guid.NewGuid();
        var reviewedAt = DateTime.UtcNow;
        var authority = new VerifiedAuthoritySnapshot(actorId, new[] { "ClassificationReviewer" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

        Assert.Throws<DocumentCompletenessRevisionOverflowException>(() => sut.RecordHumanClassificationReview(new ClassificationReviewId(Guid.NewGuid()), doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("C"), "Confirmed", actorId, reviewedAt, authority));

        Assert.Empty(sut.Reviews);
        Assert.Equal(int.MaxValue, sut.Revision);
    }

    [Fact]
    public void AuthoritySnapshot_NotRetained()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { doc }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        var actorId = Guid.NewGuid();
        var reviewedAt = DateTime.UtcNow;
        var authority = new VerifiedAuthoritySnapshot(actorId, new[] { "ClassificationReviewer" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

        sut.RecordHumanClassificationReview(new ClassificationReviewId(Guid.NewGuid()), doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("C"), "C", actorId, reviewedAt, authority);
        
        var review = sut.Reviews.Single();
        
        var properties = typeof(ClassificationReview).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var hasSnapshot = properties.Any(p => p.PropertyType == typeof(VerifiedAuthoritySnapshot));
        Assert.False(hasSnapshot, "Authority snapshot should not be retained by reference.");
    }

    [Fact]
    public void ExactEventPropertyMaps_AreCorrect()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var doc = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { doc }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        var ev = sut.DomainEvents.OfType<DocumentCompletenessAssessed>().Single();
        Assert.IsType<Guid>(ev.EventId);
        Assert.IsType<DateTime>(ev.OccurredOn);
        Assert.Equal(sut.Id, ev.DocumentCompletenessAssessmentId);
        
        var actorId = Guid.NewGuid();
        var reviewedAt = DateTime.UtcNow;
        var authority = new VerifiedAuthoritySnapshot(actorId, new[] { "ClassificationReviewer" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

        sut.RecordHumanClassificationReview(new ClassificationReviewId(Guid.NewGuid()), doc.Id, ClassificationReviewDecision.Corrected, new DocumentClassificationCode("C"), "C", actorId, reviewedAt, authority);
        
        var revEv = sut.DomainEvents.OfType<DocumentClassificationHumanReviewed>().Single();
        Assert.Equal(doc.Id, revEv.ClassifiedDocumentId);
        Assert.Equal(ClassificationReviewDecision.Corrected, revEv.ClassificationReviewDecision);
    }

    [Fact]
    public void NoPublicSettersOrRebindingAPIs()
    {
        var props = typeof(DocumentCompletenessAssessment).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
        {
            if (p.CanWrite)
            {
                var setMethod = p.GetSetMethod(false);
                Assert.Null(setMethod);
            }
        }
        
        var methods = typeof(DocumentCompletenessAssessment).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var hasRebind = methods.Any(m => m.Name.Contains("Bind", StringComparison.OrdinalIgnoreCase));
        Assert.False(hasRebind, "No rebinding API should exist.");
    }
}
