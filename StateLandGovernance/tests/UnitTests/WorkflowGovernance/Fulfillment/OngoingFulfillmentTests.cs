namespace StateLandGovernance.UnitTests.WorkflowGovernance.Fulfillment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class OngoingFulfillmentTests
{
    private static LeaseCase CreateLeaseCase()
    {
        var actorId = Guid.NewGuid();
        var leaseCaseId = new LeaseCaseId(Guid.NewGuid());
        var actionTime = DateTime.UtcNow;
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.Value.ToString("D"));
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator" },
            scope,
            actionTime.AddMinutes(-5),
            actionTime.AddMinutes(-2),
            actionTime.AddMinutes(10));

        return new LeaseCase(leaseCaseId, "APP-123", actorId, actionTime, authority);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Waiver_AuthorizedCurrentCase_PreservesSourceAndDoesNotApprove(bool condition)
    {
        var c = CreateLeaseCase(); var now = DateTime.UtcNow;
        var id = condition ? c.AddApprovalCondition(new("DS"), "Test condition").Id
            : c.AddDocumentSubmissionRequirement(new("TEST"), now.AddDays(1)).Id;
        var actor = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(actor, new[] { "FulfillmentWaiver" },
            new(AuthorityScopeKind.LeaseCase, c.Id.Value.ToString("D")), now.AddMinutes(-1), now, now.AddMinutes(1));
        var revision = c.Revision;
        Assert.Throws<InvalidFulfillmentException>(() => c.WaiveFulfillment(id, condition, revision + 1, authority, "not applicable", "review:test", now));
        c.WaiveFulfillment(id, condition, revision, authority, "not applicable", "review:test", now);
        var waiver = condition ? c.ApprovalConditions.Single().Waiver : c.DocumentSubmissionRequirements.Single().Waiver;
        Assert.Equal(actor, waiver!.ActorId); Assert.Equal("review:test", waiver.EvidenceReference);
        Assert.Equal(FulfillmentStatus.Pending, waiver.PreviousStatus);
        Assert.Equal(revision + 1, c.Revision); Assert.Equal(StateLandGovernance.WorkflowGovernance.Domain.Handoff.LeaseCaseStatus.Draft, c.Status);
        Assert.Contains(c.DomainEvents, e => e is FulfillmentWaived);
        Assert.Throws<InvalidFulfillmentException>(() => c.WaiveFulfillment(id, condition, c.Revision, authority, "again", "review:test", now));
        Assert.Throws<InvalidFulfillmentException>(() => { if (condition) c.FulfillCondition(id, now); else c.FulfillDocumentRequirement(id, new(Guid.NewGuid()), now); });
    }

    [Fact]
    public void Waiver_WithoutCapability_DoesNotChangeObligation()
    {
        var c = CreateLeaseCase(); var now = DateTime.UtcNow;
        var requirement = c.AddDocumentSubmissionRequirement(new("TEST"), now.AddDays(1));
        var authority = new VerifiedAuthoritySnapshot(Guid.NewGuid(), new[] { "LeaseInitiator" },
            new(AuthorityScopeKind.LeaseCase, c.Id.Value.ToString("D")), now.AddMinutes(-1), now, now.AddMinutes(1));
        Assert.Throws<MissingVerifiedAuthorityException>(() => c.WaiveFulfillment(requirement.Id, false, c.Revision, authority, "reason", "source", now));
        Assert.Equal(FulfillmentStatus.Pending, requirement.Status); Assert.Null(requirement.Waiver);
    }

    [Fact]
    public void LeaseCase_Can_Add_And_Fulfill_Approval_Condition()
    {
        var leaseCase = CreateLeaseCase();
        var institution = new InstitutionCode("CEA");
        var description = "Install industrial effluent treatment plant prior to commencing site operations";

        var condition = leaseCase.AddApprovalCondition(institution, description);

        Assert.Single(leaseCase.ApprovalConditions);
        Assert.Equal(FulfillmentStatus.Pending, condition.Status);
        Assert.Null(condition.FulfilledAtUtc);
        Assert.Equal(institution, condition.InstitutionCode);
        Assert.Equal(description, condition.Description);

        var fulfilledAt = DateTime.UtcNow;
        leaseCase.FulfillCondition(condition.Id, fulfilledAt);

        Assert.Equal(FulfillmentStatus.Fulfilled, condition.Status);
        Assert.Equal(fulfilledAt, condition.FulfilledAtUtc);
    }

    [Fact]
    public void LeaseCase_Can_Add_And_Fulfill_Document_Submission_Requirement()
    {
        var leaseCase = CreateLeaseCase();
        var classificationCode = new DocumentClassificationCode("CadastralSurveyPlan");
        var dueDateUtc = DateTime.UtcNow.AddDays(30);

        var requirement = leaseCase.AddDocumentSubmissionRequirement(classificationCode, dueDateUtc);

        Assert.Single(leaseCase.DocumentSubmissionRequirements);
        Assert.Equal(FulfillmentStatus.Pending, requirement.Status);
        Assert.Null(requirement.FulfilledByDocumentId);
        Assert.Null(requirement.FulfilledAtUtc);
        Assert.Equal(classificationCode, requirement.DocumentClassificationCode);
        Assert.Equal(dueDateUtc, requirement.DueDateUtc);

        var documentId = new GovernedDocumentId(Guid.NewGuid());
        var fulfilledAt = DateTime.UtcNow;
        leaseCase.FulfillDocumentRequirement(requirement.Id, documentId, fulfilledAt);

        Assert.Equal(FulfillmentStatus.Fulfilled, requirement.Status);
        Assert.Equal(documentId, requirement.FulfilledByDocumentId);
        Assert.Equal(fulfilledAt, requirement.FulfilledAtUtc);
    }

    [Fact]
    public void Fulfilling_Already_Fulfilled_Requirement_Throws_InvalidFulfillmentException()
    {
        var leaseCase = CreateLeaseCase();

        // 1. Condition fulfillment guard
        var condition = leaseCase.AddApprovalCondition(new InstitutionCode("AGR"), "Preserve buffer irrigation reservation");
        leaseCase.FulfillCondition(condition.Id, DateTime.UtcNow);

        var conditionEx = Assert.Throws<InvalidFulfillmentException>(() =>
            leaseCase.FulfillCondition(condition.Id, DateTime.UtcNow));
        Assert.Contains("already fulfilled", conditionEx.Message, StringComparison.OrdinalIgnoreCase);

        // 2. Document requirement fulfillment guard
        var requirement = leaseCase.AddDocumentSubmissionRequirement(new DocumentClassificationCode("AffidavitOfNoEncumbrance"), DateTime.UtcNow.AddDays(14));
        var documentId = new GovernedDocumentId(Guid.NewGuid());
        leaseCase.FulfillDocumentRequirement(requirement.Id, documentId, DateTime.UtcNow);

        var documentEx = Assert.Throws<InvalidFulfillmentException>(() =>
            leaseCase.FulfillDocumentRequirement(requirement.Id, documentId, DateTime.UtcNow));
        Assert.Contains("already fulfilled", documentEx.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Non-existent item guards
        var missingConditionEx = Assert.Throws<InvalidFulfillmentException>(() =>
            leaseCase.FulfillCondition(Guid.NewGuid(), DateTime.UtcNow));
        Assert.Contains("not found", missingConditionEx.Message, StringComparison.OrdinalIgnoreCase);

        var missingDocumentEx = Assert.Throws<InvalidFulfillmentException>(() =>
            leaseCase.FulfillDocumentRequirement(Guid.NewGuid(), documentId, DateTime.UtcNow));
        Assert.Contains("not found", missingDocumentEx.Message, StringComparison.OrdinalIgnoreCase);
    }
}
