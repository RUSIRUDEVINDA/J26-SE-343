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

public class DocumentCompletenessAssessmentExtendedTests
{
    private static AssessedDocumentBinding CreateBinding(Guid docId) => new(
        new GovernedDocumentId(docId),
        new DocumentVersionId(Guid.NewGuid()),
        new DocumentChecksum("SHA256", "checksum")
    );

    [Fact]
    public void Constructor_DeterministicallyOrdersBindings()
    {
        var binding2 = new AssessedDocumentBinding(new GovernedDocumentId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), new DocumentVersionId(Guid.NewGuid()), new DocumentChecksum("SHA", "1"));
        var binding1 = new AssessedDocumentBinding(new GovernedDocumentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), new DocumentVersionId(Guid.NewGuid()), new DocumentChecksum("SHA", "2"));
        
        var classification1 = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding1, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);
        var classification2 = new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding2, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow);

        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding2, binding1 }, new[] { classification2, classification1 }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        
        Assert.Equal(binding1, sut.AssessedDocuments.First());
    }

    [Fact]
    public void Constructor_DefensivelyCopiesClassifications()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var classifications = new List<ClassifiedDocument> { new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow) };
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, classifications, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow);
        classifications.Clear();
        Assert.Single(sut.ClassifiedDocuments);
    }

    [Fact]
    public void Constructor_DefensivelyCopiesRequirements()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var reqs = new List<DocumentRequirementSnapshot> { new DocumentRequirementSnapshot(new DocumentRequirementId(Guid.NewGuid()), new DocumentClassificationCode("R"), RequirementCriticality.Mandatory, RequirementApplicability.Required, 1, "C", "D") };
        var sut = new DocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", new[] { binding }, new[] { new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow) }, reqs, DateTime.UtcNow);
        reqs.Clear();
        Assert.Single(sut.Requirements);
    }

    [Fact]
    public void Classification_ReferencingAbsentBinding_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var absentBinding = CreateBinding(Guid.NewGuid());
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", 
            new[] { binding }, new[] { new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), absentBinding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow) }, 
            Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow));
    }

    [Fact]
    public void Duplicate_ClassifiedDocumentId_Throws()
    {
        var binding1 = CreateBinding(Guid.NewGuid());
        var binding2 = CreateBinding(Guid.NewGuid());
        var dupId = new ClassifiedDocumentId(Guid.NewGuid());
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", 
            new[] { binding1, binding2 }, new[] { 
                new ClassifiedDocument(dupId, binding1, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow),
                new ClassifiedDocument(dupId, binding2, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow)
            }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow));
    }

    [Fact]
    public void Duplicate_RequirementId_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var reqId = new DocumentRequirementId(Guid.NewGuid());
        var req1 = new DocumentRequirementSnapshot(reqId, new DocumentClassificationCode("R1"), RequirementCriticality.Optional, RequirementApplicability.Required, 1, "C", "D");
        var req2 = new DocumentRequirementSnapshot(reqId, new DocumentClassificationCode("R2"), RequirementCriticality.Optional, RequirementApplicability.Required, 1, "C", "D");
        
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", 
            new[] { binding }, new[] { new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow) }, 
            new[] { req1, req2 }, DateTime.UtcNow));
    }

    [Fact]
    public void Duplicate_RequirementClassificationCode_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var req1 = new DocumentRequirementSnapshot(new DocumentRequirementId(Guid.NewGuid()), new DocumentClassificationCode("R1"), RequirementCriticality.Optional, RequirementApplicability.Required, 1, "C", "D");
        var req2 = new DocumentRequirementSnapshot(new DocumentRequirementId(Guid.NewGuid()), new DocumentClassificationCode("R1"), RequirementCriticality.Optional, RequirementApplicability.Required, 1, "C", "D");
        
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()), new LeaseCaseId(Guid.NewGuid()), "Set", "v1", 
            new[] { binding }, new[] { new ClassifiedDocument(new ClassifiedDocumentId(Guid.NewGuid()), binding, null, null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow) }, 
            new[] { req1, req2 }, DateTime.UtcNow));
    }

    [Fact]
    public void Accepted_WithoutCode_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()), binding, null, new ConfidenceScore(0.9m), DocumentClassificationStatus.Accepted, null, null, null, DateTime.UtcNow));
    }

    [Fact]
    public void Accepted_WithoutConfidence_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()), binding, new DocumentClassificationCode("C"), null, DocumentClassificationStatus.Accepted, null, null, null, DateTime.UtcNow));
    }

    [Fact]
    public void Uncertain_WithoutCandidateCode_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()), binding, null, new ConfidenceScore(0.9m), DocumentClassificationStatus.Uncertain, null, null, null, DateTime.UtcNow));
    }

    [Fact]
    public void Uncertain_WithoutConfidence_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()), binding, new DocumentClassificationCode("C"), null, DocumentClassificationStatus.Uncertain, null, null, null, DateTime.UtcNow));
    }

    [Fact]
    public void Unclassified_WithCodeOrConfidence_Throws()
    {
        var binding = CreateBinding(Guid.NewGuid());
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()), binding, new DocumentClassificationCode("C"), null, DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow));
        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()), binding, null, new ConfidenceScore(0.5m), DocumentClassificationStatus.Unclassified, null, null, null, DateTime.UtcNow));
    }
}
