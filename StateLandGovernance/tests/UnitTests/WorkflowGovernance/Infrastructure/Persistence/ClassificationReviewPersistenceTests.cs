namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure.Persistence;

using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;
using Xunit;

public sealed class ClassificationReviewPersistenceTests
{
    [Fact]
    public void ConfirmedClassification_ReloadsWithItsOriginalCodeAndAuthority()
    {
        var at = DateTime.UtcNow; var actor = Guid.NewGuid();
        var binding = new AssessedDocumentBinding(new(Guid.NewGuid()), new(Guid.NewGuid()), new("SHA-256", new string('a',64)));
        var classified = new ClassifiedDocument(new(Guid.NewGuid()), binding, new("Proposal"), new(0m),
            DocumentClassificationStatus.Uncertain, null, null, null, at);
        var assessment = new DocumentCompletenessAssessment(new(Guid.NewGuid()), new(Guid.NewGuid()), "TEST", "1", [binding], [classified],
            [new(new(Guid.NewGuid()), new("Proposal"), RequirementCriticality.Mandatory, RequirementApplicability.Required, 1, "TEST", "TEST ONLY")], at);
        var authority = new VerifiedAuthoritySnapshot(actor, ["ClassificationReviewer"],
            new(AuthorityScopeKind.GovernedDocument, binding.GovernedDocumentId.Value.ToString("D")), at.AddHours(-1), at, at.AddHours(1));
        assessment.RecordHumanClassificationReview(new(Guid.NewGuid()), classified.Id, ClassificationReviewDecision.Confirmed,
            null, null, actor, at, authority);
        var restored = DocumentCompletenessAssessmentPersistenceMapper.ToDomain(DocumentCompletenessAssessmentPersistenceMapper.ToPersistence(assessment));
        Assert.Equal(CompletenessAssessmentOutcome.Complete, restored.Outcome);
        Assert.Equal(ClassificationReviewDecision.Confirmed, restored.Reviews.Single().Decision);
        Assert.Equal(actor, restored.Reviews.Single().ReviewingActorId);
        Assert.Equal("Proposal", restored.ClassifiedDocuments.Single().OriginalClassificationCode!.Value);
        Assert.Empty(restored.DomainEvents);
    }
}
