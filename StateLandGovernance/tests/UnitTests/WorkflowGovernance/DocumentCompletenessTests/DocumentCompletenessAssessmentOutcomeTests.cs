namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentCompletenessTests;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using Xunit;

public class DocumentCompletenessAssessmentOutcomeTests
{
    private static AssessedDocumentBinding CreateBinding(Guid docId) => new(
        new GovernedDocumentId(docId),
        new DocumentVersionId(Guid.NewGuid()),
        new DocumentChecksum("SHA256", "checksum")
    );

    [Fact]
    public void Outcome_AnyRequirementUndetermined_ReturnsInsufficientInformation()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("DOC"),
            new ConfidenceScore(0.95m),
            DocumentClassificationStatus.Accepted,
            null, null, null, DateTime.UtcNow
        );

        var requirement = new DocumentRequirementSnapshot(
            new DocumentRequirementId(Guid.NewGuid()),
            new DocumentClassificationCode("DOC2"),
            RequirementCriticality.Conditional,
            RequirementApplicability.Undetermined,
            1,
            "R01",
            "Desc"
        );

        var sut = new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "Set", "v1", new[] { binding }, new[] { classification }, new[] { requirement }, DateTime.UtcNow
        );

        Assert.Equal(CompletenessAssessmentOutcome.InsufficientInformation, sut.Outcome);
    }

    [Fact]
    public void Outcome_MissingRequiredButMatchableByUncertainDoc_ReturnsRequiresHumanReview()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("REQ"),
            new ConfidenceScore(0.45m), // low confidence
            DocumentClassificationStatus.Uncertain,
            null, null, null, DateTime.UtcNow
        );

        var requirement = new DocumentRequirementSnapshot(
            new DocumentRequirementId(Guid.NewGuid()),
            new DocumentClassificationCode("REQ"),
            RequirementCriticality.Mandatory,
            RequirementApplicability.Required,
            1,
            "R01",
            "Desc"
        );

        var sut = new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "Set", "v1", new[] { binding }, new[] { classification }, new[] { requirement }, DateTime.UtcNow
        );

        Assert.Equal(CompletenessAssessmentOutcome.RequiresHumanReview, sut.Outcome);
        Assert.Single(sut.MissingRequirements); // Definite missing is still retained
    }

    [Fact]
    public void Outcome_MissingRequiredWithNoPossibleMatch_ReturnsMissingRequiredDocuments()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("OTHER"),
            new ConfidenceScore(0.95m),
            DocumentClassificationStatus.Accepted,
            null, null, null, DateTime.UtcNow
        );

        var requirement = new DocumentRequirementSnapshot(
            new DocumentRequirementId(Guid.NewGuid()),
            new DocumentClassificationCode("REQ"),
            RequirementCriticality.Mandatory,
            RequirementApplicability.Required,
            1,
            "R01",
            "Desc"
        );

        var sut = new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "Set", "v1", new[] { binding }, new[] { classification }, new[] { requirement }, DateTime.UtcNow
        );

        Assert.Equal(CompletenessAssessmentOutcome.MissingRequiredDocuments, sut.Outcome);
    }
    
    [Fact]
    public void RecordReview_ValidCorrected_UpdatesEffectiveCodeAndOutcome()
    {
        var binding = CreateBinding(Guid.NewGuid());
        var classification = new ClassifiedDocument(
            new ClassifiedDocumentId(Guid.NewGuid()),
            binding,
            new DocumentClassificationCode("WRONG"),
            new ConfidenceScore(0.9m),
            DocumentClassificationStatus.Accepted,
            null, null, null, DateTime.UtcNow
        );

        var requirement = new DocumentRequirementSnapshot(
            new DocumentRequirementId(Guid.NewGuid()),
            new DocumentClassificationCode("RIGHT"),
            RequirementCriticality.Mandatory,
            RequirementApplicability.Required,
            1,
            "R01",
            "Desc"
        );

        var sut = new DocumentCompletenessAssessment(
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            "Set", "v1", new[] { binding }, new[] { classification }, new[] { requirement }, DateTime.UtcNow
        );

        Assert.Equal(CompletenessAssessmentOutcome.MissingRequiredDocuments, sut.Outcome);

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "ClassificationReviewer" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")),
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5)
        );

        sut.RecordHumanClassificationReview(
            new ClassificationReviewId(Guid.NewGuid()),
            classification.Id,
            ClassificationReviewDecision.Corrected,
            new DocumentClassificationCode("RIGHT"),
            "Was wrong",
            actorId,
            DateTime.UtcNow,
            authority
        );

        Assert.Equal(CompletenessAssessmentOutcome.Complete, sut.Outcome);
        Assert.Empty(sut.MissingRequirements);
    }
}
