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

public class DocumentCompletenessAssessmentContractTests
{
    private static AssessedDocumentBinding CreateBinding(Guid docId) => new(
        new GovernedDocumentId(docId),
        new DocumentVersionId(Guid.NewGuid()),
        new DocumentChecksum("SHA256", "checksum")
    );

    [Fact]
    public void Constructor_ValidData_CreatesAssessmentAndEvent()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var assessmentId = new DocumentCompletenessAssessmentId(Guid.NewGuid());
        
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("TEST-DOC"),
            new ConfidenceScore(0.95m),
            DocumentClassificationStatus.Accepted,
            new AnalysisRunId(Guid.NewGuid()),
            new AnalysisRunResultId(Guid.NewGuid()),
            new AnalysisModelReference("Provider", "Model", "1.0"),
            DateTime.UtcNow
        );

        var requirement = new DocumentRequirementSnapshot(
            new DocumentRequirementId(Guid.NewGuid()),
            new DocumentClassificationCode("TEST-DOC"),
            RequirementCriticality.Mandatory,
            RequirementApplicability.Required,
            1,
            "R01",
            "Desc"
        );

        var sut = new DocumentCompletenessAssessment(
            assessmentId,
            new LeaseCaseId(Guid.NewGuid()),
            "ReqSet",
            "v1",
            new[] { binding },
            new[] { classification },
            new[] { requirement },
            DateTime.UtcNow
        );

        Assert.Equal(assessmentId, sut.Id);
        Assert.Equal(CompletenessAssessmentOutcome.Complete, sut.Outcome);
        Assert.Empty(sut.MissingRequirements);
        Assert.Single(sut.DomainEvents);
        Assert.Equal(1, sut.Revision);
    }

    [Fact]
    public void Constructor_ClassificationLacksBinding_ThrowsException()
    {
        var binding1 = CreateBinding(Guid.NewGuid());
        var binding2 = CreateBinding(Guid.NewGuid()); // Not passed to assessment
        
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding2,
            new DocumentClassificationCode("TEST"),
            new ConfidenceScore(0.95m),
            DocumentClassificationStatus.Accepted,
            null, null, null, DateTime.UtcNow
        );

        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "ReqSet",
            "v1",
            new[] { binding1 },
            new[] { classification },
            Array.Empty<DocumentRequirementSnapshot>(),
            DateTime.UtcNow
        ));
    }

    [Fact]
    public void Constructor_DuplicateBinding_ThrowsException()
    {
        var binding1 = CreateBinding(Guid.NewGuid());
        
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding1,
            new DocumentClassificationCode("TEST"),
            new ConfidenceScore(0.95m),
            DocumentClassificationStatus.Accepted,
            null, null, null, DateTime.UtcNow
        );

        Assert.Throws<InvalidDocumentCompletenessAssessmentException>(() => new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "ReqSet",
            "v1",
            new[] { binding1, binding1 }, // Duplicate
            new[] { classification },
            Array.Empty<DocumentRequirementSnapshot>(),
            DateTime.UtcNow
        ));
    }

    [Fact]
    public void RecordHumanClassificationReview_ValidAuthority_UpdatesDocumentAndEmitsEvent()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var assessmentId = new DocumentCompletenessAssessmentId(Guid.NewGuid());
        
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("TEST-UNCERTAIN"),
            new ConfidenceScore(0.50m),
            DocumentClassificationStatus.Uncertain,
            null, null, null, DateTime.UtcNow
        );

        var sut = new DocumentCompletenessAssessment(
            assessmentId,
            new LeaseCaseId(Guid.NewGuid()),
            "ReqSet",
            "v1",
            new[] { binding },
            new[] { classification },
            Array.Empty<DocumentRequirementSnapshot>(),
            DateTime.UtcNow
        );

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "ClassificationReviewer" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")),
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5)
        );

        var reviewId = new ClassificationReviewId(Guid.NewGuid());
        
        sut.RecordHumanClassificationReview(
            reviewId,
            classification.Id,
            ClassificationReviewDecision.Confirmed,
            null,
            null,
            actorId,
            DateTime.UtcNow,
            authority
        );

        Assert.Single(sut.Reviews);
        Assert.Equal(2, sut.Revision);
        Assert.Equal(2, sut.DomainEvents.Count);
    }
    
    [Fact]
    public void Constructor_DefensiveCopies_AssessedBindings()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var bindings = new List<AssessedDocumentBinding> { binding };
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("TEST"),
            new ConfidenceScore(0.95m),
            DocumentClassificationStatus.Accepted,
            null, null, null, DateTime.UtcNow
        );
        var sut = new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "ReqSet", "v1", bindings, new[] { classification }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow
        );
        
        bindings.Clear();
        Assert.Single(sut.AssessedDocuments);
    }

    [Fact]
    public void RecordReview_ValidatesAuthority_ClassificationReviewerCapability()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("TEST-UNCERTAIN"),
            new ConfidenceScore(0.50m),
            DocumentClassificationStatus.Uncertain,
            null, null, null, DateTime.UtcNow
        );

        var sut = new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "ReqSet", "v1", new[] { binding }, new[] { classification }, Array.Empty<DocumentRequirementSnapshot>(), DateTime.UtcNow
        );

        var authority = new VerifiedAuthoritySnapshot(
            Guid.NewGuid(),
            new[] { "WrongCapability" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")),
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5)
        );

        Assert.Throws<MissingVerifiedAuthorityException>(() => sut.RecordHumanClassificationReview(
            new ClassificationReviewId(Guid.NewGuid()),
            classification.Id,
            ClassificationReviewDecision.Confirmed,
            null,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow,
            authority
        ));
    }
}
