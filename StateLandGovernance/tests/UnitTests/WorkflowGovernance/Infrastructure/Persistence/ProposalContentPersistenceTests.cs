namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure.Persistence;

using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;
using Xunit;

public sealed class ProposalContentPersistenceTests
{
    [Fact]
    public void ProposalAssessment_RoundTrip_PreservesBindingOptionalApplicabilityAndHistory()
    {
        var id = new ProposalTemplateId("DEV-TEST");
        var requirement = new ProposalContentRequirement(new("project_title"), ProposalRequirementKind.Field,
            "Test fixture", false, id, "1", "TEST ONLY", 1, "Commercial projects only");
        var template = new ProposalTemplate(id, "1", "Test fixture", "TEST ONLY", ProposalTemplateStatus.Active, new[] { requirement });
        var caseId = new LeaseCaseId(Guid.NewGuid());
        var binding = new ProposalSourceBinding(caseId, new GovernedDocumentId(Guid.NewGuid()),
            new DocumentVersionId(Guid.NewGuid()), new DocumentChecksum("SHA-256", new string('a', 64)), id, "1", DateTime.UtcNow);
        var result = new ProposalContentCompletenessResult(new(Guid.NewGuid()), caseId, binding, template.CreateSnapshot(),
            ProposalContentCompletenessOutcome.Complete, Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(), Array.Empty<ProposalContentRequirement>(),
            new[] { requirement }, Array.Empty<ProposalContentRequirement>(), new[] { "Test-only observation" }, sequenceNumber: 3);

        var restored = PersistenceJsonSerializer.Deserialize<List<ProposalContentCompletenessResult>>(
            PersistenceJsonSerializer.Serialize(new[] { result }))!.Single();
        Assert.Equal(result.Id, restored.Id);
        Assert.Equal(binding, restored.SourceBinding);
        Assert.Equal(template.DefinitionDigest, restored.TemplateSnapshot.DefinitionDigest);
        Assert.Equal("Commercial projects only", restored.MissingOptional.Single().ApplicabilityContext);
        Assert.False(restored.IsConfirmed);
        Assert.False(restored.CanSatisfyCompletenessGate);
        Assert.Equal(3, restored.SequenceNumber);
    }
}
