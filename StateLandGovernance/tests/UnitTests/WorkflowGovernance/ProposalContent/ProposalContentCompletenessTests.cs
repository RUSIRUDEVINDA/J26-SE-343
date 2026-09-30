namespace StateLandGovernance.UnitTests.WorkflowGovernance.ProposalContent;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.Events;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;
using Xunit;

public class ProposalContentCompletenessTests
{
    private readonly LeaseCaseId _leaseCaseId = new(Guid.NewGuid());
    private readonly GovernedDocumentId _documentId = new(Guid.NewGuid());
    private readonly DocumentVersionId _versionId = new(Guid.NewGuid());
    private readonly DocumentChecksum _checksum = new("SHA-256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    private readonly ProposalTemplateId _templateId = new("FICTIONAL-PROPOSAL-TEMPLATE-01");
    private const string TemplateVersion = "2026.1";
    private readonly DateTime _utcNow = new(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
    private readonly Guid _officerId = Guid.NewGuid();

    private ProposalSourceBinding CreateValidBinding(
        DocumentVersionId? versionId = null,
        DocumentChecksum? checksum = null,
        ProposalTemplateId? templateId = null,
        string? version = null,
        LeaseCaseId? leaseCaseId = null,
        GovernedDocumentId? documentId = null)
    {
        return new ProposalSourceBinding(
            leaseCaseId ?? _leaseCaseId,
            documentId ?? _documentId,
            versionId ?? _versionId,
            checksum ?? _checksum,
            templateId ?? _templateId,
            version ?? TemplateVersion,
            _utcNow,
            "FICTIONAL-EXTRACTION-BATCH-001"
        );
    }

    private ProposalRequirementObservation CreateObs(
        string reqId,
        ProposalObservationState state = ProposalObservationState.Present,
        ProposalSourceBinding? binding = null,
        int? pageNumber = 1,
        string? textSpan = "Valid description text span",
        string? evidenceReference = null,
        string? explanation = null)
    {
        var effectiveBinding = binding ?? CreateValidBinding();
        if (state == ProposalObservationState.Present)
        {
            return new ProposalRequirementObservation(
                new ProposalRequirementId(reqId),
                state,
                effectiveBinding,
                pageNumber: pageNumber,
                textSpan: textSpan,
                evidenceReference: evidenceReference
            );
        }
        else
        {
            return new ProposalRequirementObservation(
                new ProposalRequirementId(reqId),
                state,
                effectiveBinding,
                pageNumber: pageNumber,
                evidenceReference: evidenceReference,
                explanation: explanation ?? ("Reason for " + state)
            );
        }
    }

    private ProposalTemplate CreateValidTemplate(
        ProposalTemplateStatus status = ProposalTemplateStatus.Active,
        ProposalTemplateEffectivePeriod? effectivePeriod = null,
        string version = TemplateVersion)
    {
        var requirements = new List<ProposalContentRequirement>
        {
            new(
                new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"),
                ProposalRequirementKind.Section,
                "Fictional Project Description",
                isMandatory: true,
                _templateId,
                version,
                "FICTIONAL-POLICY-REF-01",
                orderIndex: 1
            ),
            new(
                new ProposalRequirementId("FICTIONAL_LAND_REQUIREMENT"),
                ProposalRequirementKind.Section,
                "Fictional Land Requirement",
                isMandatory: true,
                _templateId,
                version,
                "FICTIONAL-POLICY-REF-02",
                orderIndex: 2
            ),
            new(
                new ProposalRequirementId("FICTIONAL_IMPLEMENTATION_PLAN"),
                ProposalRequirementKind.Field,
                "Fictional Implementation Timeline",
                isMandatory: true,
                _templateId,
                version,
                "FICTIONAL-POLICY-REF-03",
                orderIndex: 3
            ),
            new(
                new ProposalRequirementId("FICTIONAL_FINANCIAL_CAPACITY"),
                ProposalRequirementKind.Field,
                "Fictional Financial Capacity Indicator",
                isMandatory: false,
                _templateId,
                version,
                "FICTIONAL-POLICY-REF-04",
                orderIndex: 4
            )
        };

        return new ProposalTemplate(
            _templateId,
            version,
            "Fictional Test Proposal Template",
            "FICTIONAL-GOV-GUIDE-2026",
            status,
            requirements,
            effectivePeriod
        );
    }

    private VerifiedAuthoritySnapshot CreateAuthoritySnapshot(
        Guid actorId,
        string capability = LeaseCase.ProposalReviewCapability,
        AuthorityScope? scope = null,
        DateTime? actionTime = null)
    {
        var time = actionTime ?? _utcNow;
        var effectiveScope = scope ?? new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString());

        return new VerifiedAuthoritySnapshot(
            actorId,
            new[] { capability },
            effectiveScope,
            time.AddMinutes(-30),
            time.AddMinutes(-10),
            time.AddMinutes(60)
        );
    }

    private LeaseCase CreateLeaseCase()
    {
        var initiatorId = Guid.NewGuid();
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, _leaseCaseId.Value.ToString());
        var authority = new VerifiedAuthoritySnapshot(
            initiatorId,
            new[] { "LeaseInitiator" },
            scope,
            _utcNow.AddMinutes(-30),
            _utcNow.AddMinutes(-10),
            _utcNow.AddMinutes(60)
        );

        return new LeaseCase(_leaseCaseId, "APP-REF-FICTIONAL-01", initiatorId, _utcNow, authority);
    }

    // 1. Active versioned template construction
    [Fact]
    public void Test01_ActiveVersionedTemplate_ConstructsSuccessfully()
    {
        var template = CreateValidTemplate();

        Assert.Equal(_templateId, template.Id);
        Assert.Equal(TemplateVersion, template.Version);
        Assert.Equal(ProposalTemplateStatus.Active, template.Status);
        Assert.Equal(4, template.Requirements.Count);
        Assert.True(template.IsActiveAt(_utcNow));
    }

    // 2. Duplicate requirement rejection
    [Fact]
    public void Test02_DuplicateRequirementId_WithinSameTemplateVersion_ThrowsException()
    {
        var duplicateReqs = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"), ProposalRequirementKind.Section, "Desc 1", true, _templateId, TemplateVersion, "REF-1", 1),
            new(new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"), ProposalRequirementKind.Section, "Desc 2", true, _templateId, TemplateVersion, "REF-1", 2)
        };

        var ex = Assert.Throws<InvalidProposalTemplateException>(() =>
            new ProposalTemplate(_templateId, TemplateVersion, "Template", "REF", ProposalTemplateStatus.Active, duplicateReqs));

        Assert.Contains("Duplicate requirement identifier", ex.Message);
    }

    // 3. Section and field requirements remain distinct
    [Fact]
    public void Test03_SectionAndFieldRequirements_RemainDistinct()
    {
        var secReq = new ProposalContentRequirement(new ProposalRequirementId("FICTIONAL_SEC"), ProposalRequirementKind.Section, "Section Req", true, _templateId, TemplateVersion, "REF", 1);
        var fieldReq = new ProposalContentRequirement(new ProposalRequirementId("FICTIONAL_FIELD"), ProposalRequirementKind.Field, "Field Req", true, _templateId, TemplateVersion, "REF", 2);

        Assert.NotEqual(secReq.Kind, fieldReq.Kind);
        Assert.Equal(ProposalRequirementKind.Section, secReq.Kind);
        Assert.Equal(ProposalRequirementKind.Field, fieldReq.Kind);
    }

    // 4. All mandatory content present
    [Fact]
    public void Test04_AllMandatoryContentPresent_ReturnsCompleteOutcome()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Description text span"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, evidenceReference: "REF-LAND-01"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan text span"),
            CreateObs("FICTIONAL_FINANCIAL_CAPACITY", ProposalObservationState.Present, binding, pageNumber: 4, textSpan: "Capacity span")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.Complete, result.Outcome);
        Assert.Equal(3, result.SatisfiedMandatory.Count);
        Assert.Empty(result.MissingMandatory);
        Assert.Empty(result.ReviewRequiredMandatory);
        Assert.False(result.IsConfirmed);
    }

    // 5. One mandatory section missing
    [Fact]
    public void Test05_OneMandatorySectionMissing_ReturnsIncompleteOutcome()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Description text"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Section heading not identified in document"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan text")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, result.Outcome);
        Assert.Single(result.MissingMandatory);
        Assert.Equal("FICTIONAL_LAND_REQUIREMENT", result.MissingMandatory.First().Id.Value);
    }

    // 6. One mandatory field missing
    [Fact]
    public void Test06_OneMandatoryFieldMissing_ReturnsIncompleteOutcome()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Description text"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land text"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Missing, binding, explanation: "Timeline table absent")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, result.Outcome);
        Assert.Single(result.MissingMandatory);
        Assert.Equal("FICTIONAL_IMPLEMENTATION_PLAN", result.MissingMandatory.First().Id.Value);
        Assert.Equal(ProposalRequirementKind.Field, result.MissingMandatory.First().Kind);
    }

    // 7. Multiple missing requirements
    [Fact]
    public void Test07_MultipleMissingRequirements_ReturnsIncompleteWithAllMissingListed()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Missing, binding, explanation: "Missing project desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing land req"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan text")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, result.Outcome);
        Assert.Equal(2, result.MissingMandatory.Count);
    }

    // 8. Optional requirement missing does not cause Incomplete
    [Fact]
    public void Test08_OptionalRequirementMissing_DoesNotBlockCompleteOutcome()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan"),
            CreateObs("FICTIONAL_FINANCIAL_CAPACITY", ProposalObservationState.Missing, binding, explanation: "Optional capacity not provided")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.Complete, result.Outcome);
        Assert.Single(result.MissingOptional);
        Assert.Equal("FICTIONAL_FINANCIAL_CAPACITY", result.MissingOptional.First().Id.Value);
    }

    // 9. Uncertain observation triggers HumanReviewRequired
    [Fact]
    public void Test09_UncertainObservation_TriggersHumanReviewRequiredOutcome()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Uncertain, binding, evidenceReference: "Ambiguous text in section 2", explanation: "Ambiguous content"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.ReviewRequiredMandatory);
        Assert.Equal("FICTIONAL_LAND_REQUIREMENT", result.ReviewRequiredMandatory.First().Id.Value);
    }

    // 10. Unreadable observation triggers HumanReviewRequired
    [Fact]
    public void Test10_UnreadableObservation_TriggersHumanReviewRequiredOutcome()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Unreadable, binding, pageNumber: 1, evidenceReference: "Low resolution scan", explanation: "OCR illegible"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.ReviewRequiredMandatory);
    }

    // 11. Unsupported observation triggers HumanReviewRequired
    [Fact]
    public void Test11_UnsupportedObservation_TriggersHumanReviewRequiredOutcome()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Unsupported, binding, evidenceReference: "Format unsupported", explanation: "Unsupported encoding")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.ReviewRequiredMandatory);
    }

    // 12. Duplicate observation for same requirement in same evaluation is rejected
    [Fact]
    public void Test12_DuplicateObservationForSameRequirement_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Span 1"),
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Span 2")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("Duplicate observation detected", ex.Message);
    }

    // 13. Unknown observation not in template is rejected
    [Fact]
    public void Test13_UnknownObservationNotInTemplate_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("UNKNOWN_NONEXISTENT_REQ", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Span")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("references unknown requirement", ex.Message);
    }

    // 14. Observation from the wrong document version
    [Fact]
    public void Test14_ObservationFromWrongDocumentVersion_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var wrongVersionId = new DocumentVersionId(Guid.NewGuid());
        var wrongBinding = CreateValidBinding(versionId: wrongVersionId);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, wrongBinding, pageNumber: 1, textSpan: "Span")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("references document version", ex.Message);
    }

    // 15. Observation from the wrong checksum
    [Fact]
    public void Test15_ObservationFromWrongChecksum_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var wrongChecksum = new DocumentChecksum("SHA-256", "0000000000000000000000000000000000000000000000000000000000000000");
        var wrongBinding = CreateValidBinding(checksum: wrongChecksum);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, wrongBinding, pageNumber: 1, textSpan: "Span")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("references checksum", ex.Message);
    }

    // 16. Assessment bound to exact template version
    [Fact]
    public void Test16_AssessmentBoundToExactTemplateVersion_MismatchedVersionThrowsException()
    {
        var template = CreateValidTemplate(version: "2026.1");
        var mismatchedBinding = CreateValidBinding(version: "2024.9");

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(mismatchedBinding, template, Array.Empty<ProposalRequirementObservation>()));

        Assert.Contains("does not match template", ex.Message);
    }

    // 17. Draft template
    [Fact]
    public void Test17_DraftTemplate_ReturnsHumanReviewRequiredOutcome()
    {
        var template = CreateValidTemplate(status: ProposalTemplateStatus.Draft);
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Contains("Draft", result.Explanations.First());
    }

    // 18. Awaiting-confirmation template
    [Fact]
    public void Test18_AwaitingConfirmationTemplate_ReturnsHumanReviewRequiredOutcome()
    {
        var template = CreateValidTemplate(status: ProposalTemplateStatus.AwaitingConfirmation);
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Contains("AwaitingConfirmation", result.Explanations.First());
    }

    // 19. Inactive template
    [Fact]
    public void Test19_InactiveTemplate_ReturnsUndeterminedOutcome()
    {
        var template = CreateValidTemplate(status: ProposalTemplateStatus.Inactive);
        var binding = CreateValidBinding();

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, Array.Empty<ProposalRequirementObservation>());

        Assert.Equal(ProposalContentCompletenessOutcome.Undetermined, result.Outcome);
        Assert.Contains("Inactive", result.Explanations.First());
    }

    // 20. Out-of-period template
    [Fact]
    public void Test20_OutOfPeriodTemplate_ReturnsUndeterminedOutcome()
    {
        var pastPeriod = new ProposalTemplateEffectivePeriod(_utcNow.AddYears(-2), _utcNow.AddYears(-1));
        var template = CreateValidTemplate(effectivePeriod: pastPeriod);
        var binding = CreateValidBinding();

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, Array.Empty<ProposalRequirementObservation>());

        Assert.Equal(ProposalContentCompletenessOutcome.Undetermined, result.Outcome);
        Assert.Contains("not effective", result.Explanations.First());
    }

    // 21. Deterministic evaluation regardless of input order
    [Fact]
    public void Test21_DeterministicEvaluation_RegardlessOfInputOrder()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs1 = CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc");
        var obs2 = CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land");
        var obs3 = CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan");

        var resultAscending = ProposalContentCompletenessEvaluator.Evaluate(binding, template, new[] { obs1, obs2, obs3 });
        var resultDescending = ProposalContentCompletenessEvaluator.Evaluate(binding, template, new[] { obs3, obs2, obs1 });

        Assert.Equal(resultAscending.Outcome, resultDescending.Outcome);
        Assert.Equal(resultAscending.SatisfiedMandatory.Count, resultDescending.SatisfiedMandatory.Count);
        Assert.Equal(resultAscending.MissingMandatory.Count, resultDescending.MissingMandatory.Count);
    }

    // 22. Complete, incomplete, human-review and undetermined outcomes
    [Fact]
    public void Test22_Complete_Incomplete_HumanReview_And_Undetermined_AllSupported()
    {
        var allOutcomes = Enum.GetValues<ProposalContentCompletenessOutcome>();
        Assert.Contains(ProposalContentCompletenessOutcome.Complete, allOutcomes);
        Assert.Contains(ProposalContentCompletenessOutcome.Incomplete, allOutcomes);
        Assert.Contains(ProposalContentCompletenessOutcome.HumanReviewRequired, allOutcomes);
        Assert.Contains(ProposalContentCompletenessOutcome.Undetermined, allOutcomes);
    }

    // 23. Automatic/proposed result is not treated as officer-confirmed
    [Fact]
    public void Test23_AutomaticProposedResult_IsNotTreatedAsOfficerConfirmed()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.False(result.IsConfirmed);
        Assert.Null(result.ConfirmedByActorId);
        Assert.Null(result.ConfirmedAtUtc);
        Assert.False(result.CanSatisfyCompletenessGate);
    }

    // 24. Authorised confirmation appends a new confirmed result preserving the original
    [Fact]
    public void Test24_AuthorisedOfficerConfirmation_SucceedsAndEmitsEvent()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        var authority = CreateAuthoritySnapshot(_officerId);
        var confirmed = leaseCase.ConfirmProposalContentAssessment(result.Id, authority, _officerId, _utcNow, "Confirmed by officer");

        Assert.NotEqual(result.Id, confirmed.Id);
        Assert.Equal(result.Id, confirmed.ConfirmedResultOfId);
        Assert.True(confirmed.IsConfirmed);
        Assert.Equal(_officerId, confirmed.ConfirmedByActorId);
        Assert.Equal(_utcNow, confirmed.ConfirmedAtUtc);
        Assert.True(confirmed.CanSatisfyCompletenessGate);

        // Original result is preserved unchanged
        var originalInHistory = leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(result.Id));
        Assert.False(originalInHistory.IsConfirmed);

        var confirmedEvent = leaseCase.DomainEvents.OfType<ProposalContentAssessmentConfirmed>().FirstOrDefault();
        Assert.NotNull(confirmedEvent);
        Assert.Equal(confirmed.Id, confirmedEvent.ResultId);
        Assert.Equal(_officerId, confirmedEvent.ConfirmedByActorId);
    }

    // 25. Unauthorised confirmation rejection
    [Fact]
    public void Test25_UnauthorisedConfirmation_ThrowsMissingVerifiedAuthorityException()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        var unauthorizedActor = Guid.NewGuid();
        var snapshotWithWrongActor = CreateAuthoritySnapshot(_officerId);

        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            leaseCase.ConfirmProposalContentAssessment(result.Id, snapshotWithWrongActor, unauthorizedActor, _utcNow, "Notes"));
    }

    // 26. Correction preserving the original result
    [Fact]
    public void Test26_CorrectionPreservingOriginalResult_AppendsCorrectedResult()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var initialObservations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing initially"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var initialResult = ProposalContentCompletenessEvaluator.Evaluate(binding, template, initialObservations);
        leaseCase.RecordProposalContentAssessment(initialResult);

        Assert.Single(leaseCase.ProposalContentAssessmentHistory);
        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, initialResult.Outcome);

        var correctedObservations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land section found in appendix"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var authority = CreateAuthoritySnapshot(_officerId);
        var corrected = leaseCase.CorrectProposalContentAssessment(
            initialResult.Id,
            authority,
            _officerId,
            _utcNow,
            "Officer verified land requirement in appendix A",
            correctedObservations,
            "APP-A-P4",
            template
        );

        Assert.Equal(2, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialResult.Id, corrected.SupersedesResultId);
        Assert.Equal(ProposalContentCompletenessOutcome.Complete, corrected.Outcome);
        Assert.True(corrected.IsConfirmed);

        // Verify original result is still intact in history
        var originalInHistory = leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(initialResult.Id));
        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, originalInHistory.Outcome);
        Assert.False(originalInHistory.IsConfirmed);
    }

    // 27. Correction reason and evidence requirements
    [Fact]
    public void Test27_Correction_RequiresValidReasonAndSupportingEvidence()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var initialResult = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(initialResult);

        var authority = CreateAuthoritySnapshot(_officerId);

        var ex = Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(
                initialResult.Id,
                authority,
                _officerId,
                _utcNow,
                "",
                observations,
                "EVID-01",
                template
            ));

        Assert.Contains("Correction reason is required", ex.Message);
    }

    // 28. Assessment belonging to another lease case is rejected
    [Fact]
    public void Test28_AssessmentBelongingToAnotherLeaseCase_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var otherLeaseCaseId = new LeaseCaseId(Guid.NewGuid());
        var template = CreateValidTemplate();
        var bindingForOtherCase = CreateValidBinding(leaseCaseId: otherLeaseCaseId);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingForOtherCase, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, bindingForOtherCase, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingForOtherCase, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(bindingForOtherCase, template, observations);

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            leaseCase.RecordProposalContentAssessment(result));

        Assert.Contains("does not match this lease case", ex.Message);
    }

    // 29. Duplicate assessment result is rejected
    [Fact]
    public void Test29_DuplicateAssessmentResultId_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            leaseCase.RecordProposalContentAssessment(result));

        Assert.Contains("has already been recorded", ex.Message);
    }

    // 30. Reassessment appends rather than replaces
    [Fact]
    public void Test30_Reassessment_AppendsRatherThanReplaces()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result1 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result1);

        var result2 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result2);

        Assert.Equal(2, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Contains(leaseCase.ProposalContentAssessmentHistory, a => a.Id.Equals(result1.Id));
        Assert.Contains(leaseCase.ProposalContentAssessmentHistory, a => a.Id.Equals(result2.Id));
    }

    // 31. Document-version change makes the earlier result non-current
    [Fact]
    public void Test31_DocumentVersionChange_MakesEarlierResultNonCurrent()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding(versionId: _versionId);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        var newVersionId = new DocumentVersionId(Guid.NewGuid());
        var digest = template.CreateSnapshot().DefinitionDigest;

        Assert.True(leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, TemplateVersion, digest));
        Assert.False(leaseCase.IsAssessmentCurrent(result.Id, newVersionId, _checksum, _templateId, TemplateVersion, digest));
    }

    // 32. Checksum change makes the earlier result non-current
    [Fact]
    public void Test32_ChecksumChange_MakesEarlierResultNonCurrent()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding(checksum: _checksum);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        var newChecksum = new DocumentChecksum("SHA-256", "1111111111111111111111111111111111111111111111111111111111111111");
        var digest = template.CreateSnapshot().DefinitionDigest;

        Assert.True(leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, TemplateVersion, digest));
        Assert.False(leaseCase.IsAssessmentCurrent(result.Id, _versionId, newChecksum, _templateId, TemplateVersion, digest));
    }

    // 33. Template-version change makes the earlier result non-current
    [Fact]
    public void Test33_TemplateVersionChange_MakesEarlierResultNonCurrent()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate(version: "2026.1");
        var binding = CreateValidBinding(version: "2026.1");

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        const string newTemplateVersion = "2026.2";
        var digest = template.CreateSnapshot().DefinitionDigest;

        Assert.True(leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, "2026.1", digest));
        Assert.False(leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, newTemplateVersion, digest));
    }

    // 34. Historical collection cannot be mutated
    [Fact]
    public void Test34_HistoricalCollection_CannotBeMutatedDirectly()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        var history = leaseCase.ProposalContentAssessmentHistory;
        Assert.IsAssignableFrom<IReadOnlyCollection<ProposalContentCompletenessResult>>(history);
        Assert.Throws<NotSupportedException>(() => ((ICollection<ProposalContentCompletenessResult>)history).Add(result));
    }

    // 35. Input collections are defensively copied
    [Fact]
    public void Test35_InputCollections_AreDefensivelyCopied()
    {
        var mutableReqs = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF", 1)
        };

        var template = new ProposalTemplate(_templateId, TemplateVersion, "Template", "REF", ProposalTemplateStatus.Active, mutableReqs);
        mutableReqs.Clear();

        Assert.Single(template.Requirements);
    }

    // 36. Correct Domain events
    [Fact]
    public void Test36_CorrectDomainEvents_EmittedForAssessedHumanReviewConfirmedCorrectedAndReassessed()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        // 1. First assessment with complete findings -> ProposalContentCompletenessAssessed
        var completeObservations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };
        var result1 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, completeObservations);
        leaseCase.RecordProposalContentAssessment(result1);

        var assessedEvent = leaseCase.DomainEvents.OfType<ProposalContentCompletenessAssessed>().FirstOrDefault();
        Assert.NotNull(assessedEvent);
        Assert.Equal(result1.Id, assessedEvent.ResultId);

        // 2. Reassessment with same template -> ProposalContentReassessed
        var result2 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, completeObservations);
        leaseCase.RecordProposalContentAssessment(result2);

        var reassessedEvent = leaseCase.DomainEvents.OfType<ProposalContentReassessed>().FirstOrDefault();
        Assert.NotNull(reassessedEvent);
        Assert.Equal(result1.Id, reassessedEvent.PreviousResultId);
        Assert.Equal(result2.Id, reassessedEvent.NewResultId);

        // 3. Confirmation -> ProposalContentAssessmentConfirmed
        var authority = CreateAuthoritySnapshot(_officerId);
        var confirmed = leaseCase.ConfirmProposalContentAssessment(result1.Id, authority, _officerId, _utcNow, "Notes");
        var confirmedEvent = leaseCase.DomainEvents.OfType<ProposalContentAssessmentConfirmed>().FirstOrDefault();
        Assert.NotNull(confirmedEvent);
        Assert.Equal(confirmed.Id, confirmedEvent.ResultId);

        // 4. Correction -> ProposalContentAssessmentCorrected
        var corrected = leaseCase.CorrectProposalContentAssessment(result1.Id, authority, _officerId, _utcNow, "Correction reason", completeObservations, "EVID-1", template);
        var correctedEvent = leaseCase.DomainEvents.OfType<ProposalContentAssessmentCorrected>().FirstOrDefault();
        Assert.NotNull(correctedEvent);
        Assert.Equal(result1.Id, correctedEvent.OriginalResultId);
        Assert.Equal(corrected.Id, correctedEvent.CorrectedResultId);
    }

    // 37. Rejected operations emit no events
    [Fact]
    public void Test37_RejectedOperations_EmitNoEvents()
    {
        var leaseCase = CreateLeaseCase();
        var initialEventCount = leaseCase.DomainEvents.Count;

        Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            leaseCase.RecordProposalContentAssessment(null!));

        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
    }

    // 38. Existing document-completeness behaviour remains unchanged
    [Fact]
    public void Test38_ExistingDocumentCompletenessBehaviour_RemainsUnchanged()
    {
        var binding = new AssessedDocumentBinding(
            _documentId,
            _versionId,
            _checksum
        );

        Assert.NotNull(binding);
        Assert.Equal(_documentId, binding.GovernedDocumentId);
        Assert.Equal(_versionId, binding.DocumentVersionId);
        Assert.Equal(_checksum, binding.DocumentChecksum);
    }

    // 39. Existing Batch 3K behaviour remains unchanged
    [Fact]
    public void Test39_ExistingBatch3KBehaviour_RemainsUnchanged()
    {
        var leaseCase = CreateLeaseCase();
        var assessmentResult = new RequirementAssessmentResult(
            AssessmentResultId.New(),
            _leaseCaseId,
            AssessmentSubject.FormalProposal,
            RequirementAssessmentOutcome.Required,
            "FICTIONAL-POLICY-01",
            "1.0",
            null,
            _utcNow,
            "Fictional explanation",
            "FICTIONAL-SRC-REF",
            requiresHumanConfirmation: false
        );

        leaseCase.RecordAssessmentResult(assessmentResult);

        Assert.Single(leaseCase.AssessmentHistory);
        Assert.Equal(assessmentResult.Id, leaseCase.GetLatestAssessment(AssessmentSubject.FormalProposal)?.Id);
    }

    // 40. No real proposal sections or government policies are hardcoded
    [Fact]
    public void Test40_NoRealProposalSectionsOrGovernmentPolicies_Hardcoded()
    {
        var domainAssembly = typeof(ProposalTemplate).Assembly;
        var proposalContentTypes = domainAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains("ProposalContent"))
            .ToList();

        foreach (var type in proposalContentTypes)
        {
            var stringConstants = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string) && f.IsLiteral)
                .Select(f => (string)f.GetValue(null)!)
                .ToList();

            foreach (var val in stringConstants)
            {
                Assert.DoesNotContain("LandReformCommission", val, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("CrownLands", val, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // 41. Template snapshot: Changing caller template after evaluation does not alter historical result
    [Fact]
    public void Test41_TemplateSnapshot_CallerCollectionChange_DoesNotAffectHistoricalAssessment()
    {
        var mutableReqs = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1),
            new(new ProposalRequirementId("REQ-2"), ProposalRequirementKind.Field, "Req 2", true, _templateId, TemplateVersion, "REF-2", 2)
        };
        var template = new ProposalTemplate(_templateId, TemplateVersion, "Mutable Template", "REF", ProposalTemplateStatus.Active, mutableReqs);
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("REQ-1", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Span 1"),
            CreateObs("REQ-2", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Span 2")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        var originalDigest = result.TemplateSnapshot.DefinitionDigest;

        // Mutate caller's collection
        mutableReqs.Clear();

        Assert.Equal(2, result.TemplateSnapshot.Requirements.Count);
        Assert.Equal(originalDigest, result.TemplateSnapshot.DefinitionDigest);
    }

    // 42. Template snapshot: Two templates with same ID and version but different requirements have different digests
    [Fact]
    public void Test42_TemplateSnapshot_SameIdAndVersion_DifferentRequirements_HaveDifferentDigests()
    {
        var reqsA = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var reqsB = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1),
            new(new ProposalRequirementId("REQ-2"), ProposalRequirementKind.Field, "Req 2", true, _templateId, TemplateVersion, "REF-2", 2)
        };

        var templateA = new ProposalTemplate(_templateId, TemplateVersion, "Template A", "REF-A", ProposalTemplateStatus.Active, reqsA);
        var templateB = new ProposalTemplate(_templateId, TemplateVersion, "Template B", "REF-B", ProposalTemplateStatus.Active, reqsB);

        var snapshotA = templateA.CreateSnapshot();
        var snapshotB = templateB.CreateSnapshot();

        Assert.Equal(snapshotA.TemplateId, snapshotB.TemplateId);
        Assert.Equal(snapshotA.TemplateVersion, snapshotB.TemplateVersion);
        Assert.NotEqual(snapshotA.DefinitionDigest, snapshotB.DefinitionDigest);
    }

    // 43. Missing observation: Absent observation for mandatory requirement produces HumanReviewRequired
    [Fact]
    public void Test43_MissingObservation_AbsentObservationForMandatoryRequirement_ProducesHumanReviewRequired()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        // 3 mandatory requirements in template; only 2 provided as observations
        var partialObservations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land")
            // FICTIONAL_IMPLEMENTATION_PLAN is completely omitted
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, partialObservations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.ReviewRequiredMandatory);
        Assert.Equal("FICTIONAL_IMPLEMENTATION_PLAN", result.ReviewRequiredMandatory.First().Id.Value);
        Assert.Contains("has no extraction observation", result.Explanations.First(e => e.Contains("FICTIONAL_IMPLEMENTATION_PLAN")));
    }

    // 44. Mixed finding precedence: Missing plus Uncertain produces HumanReviewRequired while retaining Missing
    [Fact]
    public void Test44_MixedFindingPrecedence_MissingPlusUncertain_ProducesHumanReviewRequiredAndRetainsMissing()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Confirmed missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Uncertain, binding, evidenceReference: "Ambiguous date", explanation: "Ambiguous date")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.MissingMandatory);
        Assert.Equal("FICTIONAL_LAND_REQUIREMENT", result.MissingMandatory.First().Id.Value);
        Assert.Single(result.ReviewRequiredMandatory);
        Assert.Equal("FICTIONAL_IMPLEMENTATION_PLAN", result.ReviewRequiredMandatory.First().Id.Value);
    }

    // 45. Mixed finding precedence: Missing plus Unreadable produces HumanReviewRequired
    [Fact]
    public void Test45_MixedFindingPrecedence_MissingPlusUnreadable_ProducesHumanReviewRequired()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Confirmed missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Unreadable, binding, evidenceReference: "Smudged text", explanation: "Smudged text")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.MissingMandatory);
        Assert.Single(result.ReviewRequiredMandatory);
    }

    // 46. Mixed finding precedence: Missing plus Unsupported produces HumanReviewRequired
    [Fact]
    public void Test46_MixedFindingPrecedence_MissingPlusUnsupported_ProducesHumanReviewRequired()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Confirmed missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Unsupported, binding, evidenceReference: "Unsupported diagram", explanation: "Unsupported diagram")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.MissingMandatory);
        Assert.Single(result.ReviewRequiredMandatory);
    }

    // 47. Mixed finding precedence: Missing plus absent observation produces HumanReviewRequired
    [Fact]
    public void Test47_MixedFindingPrecedence_MissingPlusAbsentObservation_ProducesHumanReviewRequired()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Missing, binding, explanation: "Confirmed missing"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        Assert.Equal(ProposalContentCompletenessOutcome.HumanReviewRequired, result.Outcome);
        Assert.Single(result.MissingMandatory);
        Assert.Single(result.ReviewRequiredMandatory);
    }

    // 48. Observation source binding: Wrong LeaseCaseId rejected
    [Fact]
    public void Test48_ObservationSourceBinding_WrongLeaseCaseId_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var wrongCaseId = new LeaseCaseId(Guid.NewGuid());
        var wrongBinding = CreateValidBinding(leaseCaseId: wrongCaseId);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, wrongBinding, pageNumber: 1, textSpan: "Desc")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("references LeaseCaseId", ex.Message);
    }

    // 49. Observation source binding: Wrong GovernedDocumentId rejected
    [Fact]
    public void Test49_ObservationSourceBinding_WrongGovernedDocumentId_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var wrongDocId = new GovernedDocumentId(Guid.NewGuid());
        var wrongBinding = CreateValidBinding(documentId: wrongDocId);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, wrongBinding, pageNumber: 1, textSpan: "Desc")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("references GovernedDocumentId", ex.Message);
    }

    // 50. Observation source binding: Wrong TemplateId rejected
    [Fact]
    public void Test50_ObservationSourceBinding_WrongTemplateId_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var wrongTemplateId = new ProposalTemplateId("DIFFERENT-TEMPLATE-ID");
        var wrongBinding = CreateValidBinding(templateId: wrongTemplateId);

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, wrongBinding, pageNumber: 1, textSpan: "Desc")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("references template", ex.Message);
    }

    // 51. Observation source binding: Wrong TemplateVersion rejected
    [Fact]
    public void Test51_ObservationSourceBinding_WrongTemplateVersion_ThrowsException()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var wrongBinding = CreateValidBinding(version: "9999.0");

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, wrongBinding, pageNumber: 1, textSpan: "Desc")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations));

        Assert.Contains("references template version", ex.Message);
    }

    // 52. Present evidence: Page number alone without text or evidence reference is rejected
    [Fact]
    public void Test52_PresentEvidence_PageNumberAloneWithoutEvidence_ThrowsException()
    {
        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            new ProposalRequirementObservation(
                new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"),
                ProposalObservationState.Present,
                CreateValidBinding(),
                pageNumber: 3,
                textSpan: null,
                evidenceReference: null
            ));

        Assert.Contains("must retain a non-empty evidence reference or text span", ex.Message);
    }

    // 53. Non-present observation without explanation is rejected
    [Fact]
    public void Test53_NonPresentObservation_WithoutExplanation_ThrowsException()
    {
        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            new ProposalRequirementObservation(
                new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"),
                ProposalObservationState.Missing,
                CreateValidBinding(),
                explanation: ""
            ));

        Assert.Contains("must contain a non-empty explanation or reason", ex.Message);
    }

    // 54. Confirmation preserves original findings: Confirming incomplete proposal preserves Incomplete
    [Fact]
    public void Test54_Confirmation_ConfirmingIncompleteResult_PreservesIncompleteOutcome()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Confirmed missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(result);

        var authority = CreateAuthoritySnapshot(_officerId);
        var confirmed = leaseCase.ConfirmProposalContentAssessment(result.Id, authority, _officerId, _utcNow, "Officer confirms incomplete");

        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, confirmed.Outcome);
        Assert.False(confirmed.CanSatisfyCompletenessGate);
    }

    // 55. Correction: Attempt to correct an already superseded result is rejected
    [Fact]
    public void Test55_Correction_AlreadySupersededResult_ThrowsException()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing initially"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var initialResult = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(initialResult);

        var authority = CreateAuthoritySnapshot(_officerId);
        var correctedObservations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Found in appendix"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        // First correction succeeds
        leaseCase.CorrectProposalContentAssessment(
            initialResult.Id,
            authority,
            _officerId,
            _utcNow,
            "First correction",
            correctedObservations,
            "REF-1",
            template
        );

        // Second attempt to correct the already-superseded initialResult must be rejected
        var ex = Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(
                initialResult.Id,
                authority,
                _officerId,
                _utcNow,
                "Attempt second branch correction",
                correctedObservations,
                "REF-2",
                template
            ));

        Assert.Contains("already been superseded", ex.Message);
    }

    // 56. Correction: Recalculates outcome deterministically, cannot force Complete when mandatory is missing
    [Fact]
    public void Test56_Correction_RecalculatesDeterministically_CannotForceComplete()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing initially"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var initialResult = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        leaseCase.RecordProposalContentAssessment(initialResult);

        var authority = CreateAuthoritySnapshot(_officerId);

        var correctedObservationsStillMissing = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Still missing after review"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var corrected = leaseCase.CorrectProposalContentAssessment(
            initialResult.Id,
            authority,
            _officerId,
            _utcNow,
            "Officer notes still missing",
            correctedObservationsStillMissing,
            "REF-STILL-MISSING",
            template
        );

        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, corrected.Outcome);
        Assert.False(corrected.CanSatisfyCompletenessGate);
    }

    // 57. Current-result selection: Same timestamp tie-broken deterministically by sequence number
    [Fact]
    public void Test57_CurrentResultSelection_SameTimestamp_TieBrokenBySequenceNumber()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var observations = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result1 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);
        var result2 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, observations);

        leaseCase.RecordProposalContentAssessment(result1);
        leaseCase.RecordProposalContentAssessment(result2);

        var latest = leaseCase.GetLatestRecordedProposalContentAssessment(_templateId);

        Assert.NotNull(latest);
        Assert.Equal(result2.Id, latest.Id);
        Assert.True(latest.SequenceNumber > 1);
    }

    // ==========================================
    // SECTION 1: OBSERVATION BINDING INVARIANTS
    // ==========================================

    // 58. Unbound observation cannot be evaluated (null source binding cannot be created)
    [Fact]
    public void Test58_ObservationBinding_UnboundObservation_ThrowsException()
    {
        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            new ProposalRequirementObservation(
                new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"),
                ProposalObservationState.Present,
                sourceBinding: null!,
                pageNumber: 1,
                textSpan: "Desc"
            ));

        Assert.Contains("ProposalSourceBinding is required", ex.Message);
    }

    // 59. Partially bound observation cannot be created
    [Fact]
    public void Test59_ObservationBinding_PartiallyBoundSourceBinding_CannotBeCreated()
    {
        // 1. Empty LeaseCaseId
        Assert.Throws<InvalidProposalSourceBindingException>(() =>
            new ProposalSourceBinding(
                default,
                _documentId,
                _versionId,
                _checksum,
                _templateId,
                TemplateVersion,
                _utcNow
            ));

        // 2. Empty GovernedDocumentId
        Assert.Throws<InvalidProposalSourceBindingException>(() =>
            new ProposalSourceBinding(
                _leaseCaseId,
                default,
                _versionId,
                _checksum,
                _templateId,
                TemplateVersion,
                _utcNow
            ));

        // 3. Empty DocumentVersionId
        Assert.Throws<InvalidProposalSourceBindingException>(() =>
            new ProposalSourceBinding(
                _leaseCaseId,
                _documentId,
                default,
                _checksum,
                _templateId,
                TemplateVersion,
                _utcNow
            ));

        // 4. Null DocumentChecksum
        Assert.Throws<InvalidProposalSourceBindingException>(() =>
            new ProposalSourceBinding(
                _leaseCaseId,
                _documentId,
                _versionId,
                null!,
                _templateId,
                TemplateVersion,
                _utcNow
            ));

        // 5. Empty TemplateId
        Assert.Throws<InvalidProposalSourceBindingException>(() =>
            new ProposalSourceBinding(
                _leaseCaseId,
                _documentId,
                _versionId,
                _checksum,
                default,
                TemplateVersion,
                _utcNow
            ));

        // 6. Empty TemplateVersion
        Assert.Throws<InvalidProposalSourceBindingException>(() =>
            new ProposalSourceBinding(
                _leaseCaseId,
                _documentId,
                _versionId,
                _checksum,
                _templateId,
                "",
                _utcNow
            ));
    }

    // 60. Observations cannot cross lease cases
    [Fact]
    public void Test60_ObservationBinding_CannotCrossLeaseCases()
    {
        var template = CreateValidTemplate();
        var assessmentBinding = CreateValidBinding();
        var otherCaseId = new LeaseCaseId(Guid.NewGuid());
        var foreignBinding = CreateValidBinding(leaseCaseId: otherCaseId);

        var obs = CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, foreignBinding, textSpan: "Desc");

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(assessmentBinding, template, new[] { obs }));

        Assert.Contains("references LeaseCaseId", ex.Message);
    }

    // 61. Observations cannot cross documents
    [Fact]
    public void Test61_ObservationBinding_CannotCrossDocuments()
    {
        var template = CreateValidTemplate();
        var assessmentBinding = CreateValidBinding();
        var otherDocId = new GovernedDocumentId(Guid.NewGuid());
        var foreignBinding = CreateValidBinding(documentId: otherDocId);

        var obs = CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, foreignBinding, textSpan: "Desc");

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(assessmentBinding, template, new[] { obs }));

        Assert.Contains("references GovernedDocumentId", ex.Message);
    }

    // 62. Observations cannot cross document versions
    [Fact]
    public void Test62_ObservationBinding_CannotCrossDocumentVersions()
    {
        var template = CreateValidTemplate();
        var assessmentBinding = CreateValidBinding();
        var otherVersionId = new DocumentVersionId(Guid.NewGuid());
        var foreignBinding = CreateValidBinding(versionId: otherVersionId);

        var obs = CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, foreignBinding, textSpan: "Desc");

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(assessmentBinding, template, new[] { obs }));

        Assert.Contains("references document version", ex.Message);
    }

    // 63. Observations cannot cross checksums
    [Fact]
    public void Test63_ObservationBinding_CannotCrossChecksums()
    {
        var template = CreateValidTemplate();
        var assessmentBinding = CreateValidBinding();
        var otherChecksum = new DocumentChecksum("SHA-256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var foreignBinding = CreateValidBinding(checksum: otherChecksum);

        var obs = CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, foreignBinding, textSpan: "Desc");

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(assessmentBinding, template, new[] { obs }));

        Assert.Contains("references checksum", ex.Message);
    }

    // 64. Observations cannot cross template IDs
    [Fact]
    public void Test64_ObservationBinding_CannotCrossTemplateIds()
    {
        var template = CreateValidTemplate();
        var assessmentBinding = CreateValidBinding();
        var otherTemplateId = new ProposalTemplateId("CROSS-TEMPLATE-99");
        var foreignBinding = CreateValidBinding(templateId: otherTemplateId);

        var obs = CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, foreignBinding, textSpan: "Desc");

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(assessmentBinding, template, new[] { obs }));

        Assert.Contains("references template", ex.Message);
    }

    // 65. Observations cannot cross template versions
    [Fact]
    public void Test65_ObservationBinding_CannotCrossTemplateVersions()
    {
        var template = CreateValidTemplate();
        var assessmentBinding = CreateValidBinding();
        var foreignBinding = CreateValidBinding(version: "2099.99");

        var obs = CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, foreignBinding, textSpan: "Desc");

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            ProposalContentCompletenessEvaluator.Evaluate(assessmentBinding, template, new[] { obs }));

        Assert.Contains("references template version", ex.Message);
    }

    // ==========================================
    // SECTION 2: TEMPLATE-DEFINITION DIGEST TESTS
    // ==========================================

    [Fact]
    public void Test66_TemplateDigest_AuthoritativeSourceChange_ChangesDigest()
    {
        var reqs = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE-A", ProposalTemplateStatus.Active, reqs);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE-B", ProposalTemplateStatus.Active, reqs);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test67_TemplateDigest_EffectivePeriodChange_ChangesDigest()
    {
        var reqs = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var p1 = new ProposalTemplateEffectivePeriod(_utcNow.AddDays(-10), _utcNow.AddDays(10));
        var p2 = new ProposalTemplateEffectivePeriod(_utcNow.AddDays(-5), _utcNow.AddDays(15));

        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs, p1);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs, p2);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test68_TemplateDigest_StatusChange_ChangesDigest()
    {
        var reqs = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.AwaitingConfirmation, reqs);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test69_TemplateDigest_RequirementDescriptionChange_ChangesDigest()
    {
        var reqs1 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Description Alpha", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var reqs2 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Description Beta", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs1);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs2);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test70_TemplateDigest_RequirementMandatoryStatusChange_ChangesDigest()
    {
        var reqs1 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var reqs2 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", false, _templateId, TemplateVersion, "REF-1", 1)
        };
        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs1);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs2);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test71_TemplateDigest_RequirementOrderChange_ChangesDigest()
    {
        var reqs1 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1),
            new(new ProposalRequirementId("REQ-2"), ProposalRequirementKind.Field, "Req 2", true, _templateId, TemplateVersion, "REF-2", 2)
        };
        var reqs2 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 2),
            new(new ProposalRequirementId("REQ-2"), ProposalRequirementKind.Field, "Req 2", true, _templateId, TemplateVersion, "REF-2", 1)
        };
        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs1);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs2);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test72_TemplateDigest_RequirementIdChange_ChangesDigest()
    {
        var reqs1 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-ALPHA"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var reqs2 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-BETA"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs1);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs2);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test73_TemplateDigest_RequirementKindChange_ChangesDigest()
    {
        var reqs1 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var reqs2 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Field, "Req 1", true, _templateId, TemplateVersion, "REF-1", 1)
        };
        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs1);
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, reqs2);

        Assert.NotEqual(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    [Fact]
    public void Test74_TemplateDigest_IdenticalDefinitionsInDifferentOrders_ProduceSameDigest()
    {
        var req1 = new ProposalContentRequirement(new ProposalRequirementId("REQ-A"), ProposalRequirementKind.Section, "Desc A", true, _templateId, TemplateVersion, "REF-A", 1);
        var req2 = new ProposalContentRequirement(new ProposalRequirementId("REQ-B"), ProposalRequirementKind.Field, "Desc B", true, _templateId, TemplateVersion, "REF-B", 2);

        var t1 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, new[] { req1, req2 });
        var t2 = new ProposalTemplate(_templateId, TemplateVersion, "Template", "SOURCE", ProposalTemplateStatus.Active, new[] { req2, req1 });

        Assert.Equal(t1.CreateSnapshot().DefinitionDigest, t2.CreateSnapshot().DefinitionDigest);
    }

    // =========================================================
    // SECTION 3: DISTINGUISH LATEST-RECORDED FROM CURRENT-APPLICABLE
    // =========================================================

    [Fact]
    public void Test75_LateConfirmationOfOlderVersion_DoesNotBecomeCurrentForNewerVersion()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();

        // 1. Doc version 1 assessed
        var version1Id = new DocumentVersionId(Guid.NewGuid());
        var bindingV1 = CreateValidBinding(versionId: version1Id);
        var obsV1 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingV1, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, bindingV1, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingV1, pageNumber: 3, textSpan: "Plan")
        };
        var resultV1 = ProposalContentCompletenessEvaluator.Evaluate(bindingV1, template, obsV1);
        leaseCase.RecordProposalContentAssessment(resultV1);

        // 2. Doc version 2 reassessed
        var version2Id = new DocumentVersionId(Guid.NewGuid());
        var bindingV2 = CreateValidBinding(versionId: version2Id);
        var obsV2 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingV2, pageNumber: 1, textSpan: "Desc v2"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, bindingV2, pageNumber: 2, textSpan: "Land v2"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingV2, pageNumber: 3, textSpan: "Plan v2")
        };
        var resultV2 = ProposalContentCompletenessEvaluator.Evaluate(bindingV2, template, obsV2);
        leaseCase.RecordProposalContentAssessment(resultV2);

        // 3. Officer later confirms old version 1 assessment
        var lateTime = _utcNow.AddHours(2);
        var authority = CreateAuthoritySnapshot(_officerId, actionTime: lateTime);
        var confirmedV1 = leaseCase.ConfirmProposalContentAssessment(resultV1.Id, authority, _officerId, lateTime, "Late confirmation of v1");

        // Confirmed v1 receives highest sequence number
        Assert.True(confirmedV1.SequenceNumber > resultV2.SequenceNumber);

        // GetLatestRecorded returns confirmedV1 (latest in history)
        var latestRecorded = leaseCase.GetLatestRecordedProposalContentAssessment(_templateId);
        Assert.NotNull(latestRecorded);
        Assert.Equal(confirmedV1.Id, latestRecorded.Id);

        // But GetCurrentProposalContentAssessment for current document binding (version 2) returns resultV2, NOT confirmedV1!
        var templateSnapshot = template.CreateSnapshot();
        var currentForV2 = leaseCase.GetCurrentProposalContentAssessment(bindingV2, templateSnapshot.DefinitionDigest);
        Assert.NotNull(currentForV2);
        Assert.Equal(resultV2.Id, currentForV2.Id);
        Assert.Equal(version2Id, currentForV2.SourceBinding.DocumentVersionId);

        Assert.False(leaseCase.IsAssessmentCurrent(confirmedV1.Id, bindingV2, templateSnapshot.DefinitionDigest));
        Assert.True(leaseCase.IsAssessmentCurrent(resultV2.Id, bindingV2, templateSnapshot.DefinitionDigest));
    }

    [Fact]
    public void Test76_LateCorrectionOfOlderVersion_DoesNotBecomeCurrentForNewerVersion()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();

        var version1Id = new DocumentVersionId(Guid.NewGuid());
        var bindingV1 = CreateValidBinding(versionId: version1Id);
        var obsV1 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingV1, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, bindingV1, explanation: "Missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingV1, pageNumber: 3, textSpan: "Plan")
        };
        var resultV1 = ProposalContentCompletenessEvaluator.Evaluate(bindingV1, template, obsV1);
        leaseCase.RecordProposalContentAssessment(resultV1);

        var version2Id = new DocumentVersionId(Guid.NewGuid());
        var bindingV2 = CreateValidBinding(versionId: version2Id);
        var obsV2 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingV2, pageNumber: 1, textSpan: "Desc v2"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, bindingV2, pageNumber: 2, textSpan: "Land v2"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingV2, pageNumber: 3, textSpan: "Plan v2")
        };
        var resultV2 = ProposalContentCompletenessEvaluator.Evaluate(bindingV2, template, obsV2);
        leaseCase.RecordProposalContentAssessment(resultV2);

        // Later correction of v1
        var lateTime = _utcNow.AddHours(3);
        var authority = CreateAuthoritySnapshot(_officerId, actionTime: lateTime);
        var correctedObsV1 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingV1, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, bindingV1, pageNumber: 2, textSpan: "Found in v1"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingV1, pageNumber: 3, textSpan: "Plan")
        };
        var correctedV1 = leaseCase.CorrectProposalContentAssessment(
            resultV1.Id,
            authority,
            _officerId,
            lateTime,
            "Late correction of v1",
            correctedObsV1,
            "REF-CORR-V1",
            template
        );

        Assert.True(correctedV1.SequenceNumber > resultV2.SequenceNumber);

        // Current for v2 is still resultV2
        var templateSnapshot = template.CreateSnapshot();
        var currentForV2 = leaseCase.GetCurrentProposalContentAssessment(bindingV2, templateSnapshot.DefinitionDigest);
        Assert.NotNull(currentForV2);
        Assert.Equal(resultV2.Id, currentForV2.Id);
    }

    [Fact]
    public void Test77_NewerReassessment_RemainsCurrent()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result1 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result1);

        var result2 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result2);

        var templateSnapshot = template.CreateSnapshot();
        var current = leaseCase.GetCurrentProposalContentAssessment(binding, templateSnapshot.DefinitionDigest);
        Assert.NotNull(current);
        Assert.Equal(result2.Id, current.Id);
    }

    [Fact]
    public void Test78_ConfirmationLinkedToCurrentProposedResult_IsSelectedAsCurrent()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var authority = CreateAuthoritySnapshot(_officerId);
        var confirmed = leaseCase.ConfirmProposalContentAssessment(result.Id, authority, _officerId, _utcNow, "Confirmed");

        var templateSnapshot = template.CreateSnapshot();
        var current = leaseCase.GetCurrentProposalContentAssessment(binding, templateSnapshot.DefinitionDigest);
        Assert.NotNull(current);
        Assert.Equal(confirmed.Id, current.Id);
        Assert.True(current.IsConfirmed);
    }

    [Fact]
    public void Test79_ConfirmationLinkedToStaleProposedResult_RemainsHistorical()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();

        var v1Id = new DocumentVersionId(Guid.NewGuid());
        var bindingV1 = CreateValidBinding(versionId: v1Id);
        var obsV1 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingV1, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, bindingV1, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingV1, pageNumber: 3, textSpan: "Plan")
        };
        var resultV1 = ProposalContentCompletenessEvaluator.Evaluate(bindingV1, template, obsV1);
        leaseCase.RecordProposalContentAssessment(resultV1);

        var authority = CreateAuthoritySnapshot(_officerId);
        var confirmedV1 = leaseCase.ConfirmProposalContentAssessment(resultV1.Id, authority, _officerId, _utcNow, "Confirmed V1");

        // Now doc v2 comes along
        var v2Id = new DocumentVersionId(Guid.NewGuid());
        var bindingV2 = CreateValidBinding(versionId: v2Id);
        var obsV2 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, bindingV2, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, bindingV2, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, bindingV2, pageNumber: 3, textSpan: "Plan")
        };
        var resultV2 = ProposalContentCompletenessEvaluator.Evaluate(bindingV2, template, obsV2);
        leaseCase.RecordProposalContentAssessment(resultV2);

        // confirmedV1 is historical when querying for v2
        var templateSnapshot = template.CreateSnapshot();
        var currentV2 = leaseCase.GetCurrentProposalContentAssessment(bindingV2, templateSnapshot.DefinitionDigest);
        Assert.NotNull(currentV2);
        Assert.Equal(resultV2.Id, currentV2.Id);

        // When specifically querying bindingV1, confirmedV1 is returned
        var currentV1 = leaseCase.GetCurrentProposalContentAssessment(bindingV1, templateSnapshot.DefinitionDigest);
        Assert.NotNull(currentV1);
        Assert.Equal(confirmedV1.Id, currentV1.Id);
    }

    [Fact]
    public void Test80_EqualTimestamps_DoNotAffectDeterministicSelection()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result1 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        var result2 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);

        leaseCase.RecordProposalContentAssessment(result1);
        leaseCase.RecordProposalContentAssessment(result2);

        var templateSnapshot = template.CreateSnapshot();
        var current = leaseCase.GetCurrentProposalContentAssessment(binding, templateSnapshot.DefinitionDigest);
        Assert.NotNull(current);
        Assert.Equal(result2.Id, current.Id);
    }

    // ========================================================
    // SECTION 4: CONFIRMATION AND CORRECTION BINDING SAFEGUARDS
    // ========================================================

    [Fact]
    public void Test81_Correction_WithDifferentChecksum_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var authority = CreateAuthoritySnapshot(_officerId);
        var differentChecksum = new DocumentChecksum("SHA-256", "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        var differentBinding = CreateValidBinding(checksum: differentChecksum);

        var invalidCorrectedObs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, differentBinding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, differentBinding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, differentBinding, pageNumber: 3, textSpan: "Plan")
        };

        var ex = Assert.Throws<InvalidProposalObservationException>(() =>
            leaseCase.CorrectProposalContentAssessment(
                result.Id,
                authority,
                _officerId,
                _utcNow,
                "Correction reason",
                invalidCorrectedObs,
                "REF",
                template
            ));

        Assert.Contains("references checksum", ex.Message);
    }

    [Fact]
    public void Test82_Correction_WithDifferentTemplateVersion_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var templateV1 = CreateValidTemplate(version: "2026.1");
        var templateV2 = CreateValidTemplate(version: "2026.2");
        var binding = CreateValidBinding(version: "2026.1");

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, templateV1, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var authority = CreateAuthoritySnapshot(_officerId);
        var correctedObs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var ex = Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(
                result.Id,
                authority,
                _officerId,
                _utcNow,
                "Correction reason",
                correctedObs,
                "REF",
                templateV2
            ));

        Assert.Contains("must use the exact same template ID and version", ex.Message);
    }

    [Fact]
    public void Test83_Correction_WithDifferentTemplateDigest_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var reqs1 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Original Req", true, _templateId, TemplateVersion, "REF", 1)
        };
        var templateOriginal = new ProposalTemplate(_templateId, TemplateVersion, "Template", "REF", ProposalTemplateStatus.Active, reqs1);
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("REQ-1", ProposalObservationState.Missing, binding, explanation: "Missing")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, templateOriginal, obs);
        leaseCase.RecordProposalContentAssessment(result);

        // New template instance with same ID and version but modified requirements (different digest)
        var reqs2 = new List<ProposalContentRequirement>
        {
            new(new ProposalRequirementId("REQ-1"), ProposalRequirementKind.Section, "Modified Req Text", true, _templateId, TemplateVersion, "REF", 1)
        };
        var templateModified = new ProposalTemplate(_templateId, TemplateVersion, "Template", "REF", ProposalTemplateStatus.Active, reqs2);

        var authority = CreateAuthoritySnapshot(_officerId);
        var correctedObs = new List<ProposalRequirementObservation>
        {
            CreateObs("REQ-1", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Found")
        };

        var ex = Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(
                result.Id,
                authority,
                _officerId,
                _utcNow,
                "Reason",
                correctedObs,
                "REF",
                templateModified
            ));

        Assert.Contains("Correction template definition digest", ex.Message);
    }

    [Fact]
    public void Test84_Correction_UnauthorisedActor_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var unauthorizedActor = Guid.NewGuid();
        var snapshotWithWrongActor = CreateAuthoritySnapshot(_officerId);
        var correctedObs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            leaseCase.CorrectProposalContentAssessment(
                result.Id,
                snapshotWithWrongActor,
                unauthorizedActor,
                _utcNow,
                "Correction",
                correctedObs,
                "REF",
                template
            ));
    }

    [Fact]
    public void Test85_Confirmation_DoesNotMutateOriginalResult()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var original = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(original);

        var authority = CreateAuthoritySnapshot(_officerId);
        var confirmed = leaseCase.ConfirmProposalContentAssessment(original.Id, authority, _officerId, _utcNow, "Confirmed notes");

        // Assert original result in history remains unconfirmed and unchanged
        var originalInHistory = leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(original.Id));
        Assert.False(originalInHistory.IsConfirmed);
        Assert.Null(originalInHistory.ConfirmedByActorId);
        Assert.Null(originalInHistory.ConfirmedAtUtc);
        Assert.Null(originalInHistory.ConfirmationNotes);
        Assert.Equal(1, originalInHistory.SequenceNumber);

        // Confirmed result is a distinct record
        Assert.True(confirmed.IsConfirmed);
        Assert.Equal(2, confirmed.SequenceNumber);
        Assert.Equal(_officerId, confirmed.ConfirmedByActorId);
    }

    [Fact]
    public void Test86_Correction_DoesNotMutateOriginalResult()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding, explanation: "Missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var original = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(original);

        var authority = CreateAuthoritySnapshot(_officerId);
        var correctedObs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land found"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var corrected = leaseCase.CorrectProposalContentAssessment(
            original.Id,
            authority,
            _officerId,
            _utcNow,
            "Found land section",
            correctedObs,
            "REF-APP-1",
            template
        );

        // Original result is unmutated
        var originalInHistory = leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(original.Id));
        Assert.Equal(ProposalContentCompletenessOutcome.Incomplete, originalInHistory.Outcome);
        Assert.False(originalInHistory.IsConfirmed);
        Assert.Null(originalInHistory.SupersedesResultId);
        Assert.Equal(1, originalInHistory.SequenceNumber);

        // Corrected result is a distinct record
        Assert.Equal(ProposalContentCompletenessOutcome.Complete, corrected.Outcome);
        Assert.Equal(original.Id, corrected.SupersedesResultId);
        Assert.Equal(2, corrected.SequenceNumber);
    }

    // ==========================================
    // SECTION 5: OPTIONAL UNRESOLVED CONTENT
    // ==========================================

    [Fact]
    public void Test87_OptionalRequirement_Uncertain_Unreadable_Unsupported_DoesNotBlockComplete()
    {
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        // 3 mandatory requirements Present; 1 optional requirement Uncertain/Unreadable/Unsupported
        var obsUncertain = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan"),
            CreateObs("FICTIONAL_FINANCIAL_CAPACITY", ProposalObservationState.Uncertain, binding, evidenceReference: "Optional note ambiguous", explanation: "Ambiguous optional financial")
        };

        var resultUncertain = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obsUncertain);

        // Outcome must remain Complete because mandatory content is complete!
        Assert.Equal(ProposalContentCompletenessOutcome.Complete, resultUncertain.Outcome);
        Assert.Empty(resultUncertain.ReviewRequiredMandatory);
        Assert.Single(resultUncertain.UnresolvedOptional);
        Assert.Equal("FICTIONAL_FINANCIAL_CAPACITY", resultUncertain.UnresolvedOptional.First().Id.Value);

        // Test with Unreadable
        var obsUnreadable = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan"),
            CreateObs("FICTIONAL_FINANCIAL_CAPACITY", ProposalObservationState.Unreadable, binding, evidenceReference: "Unreadable scan", explanation: "Blurry optional capacity")
        };

        var resultUnreadable = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obsUnreadable);
        Assert.Equal(ProposalContentCompletenessOutcome.Complete, resultUnreadable.Outcome);
        Assert.Empty(resultUnreadable.ReviewRequiredMandatory);
        Assert.Single(resultUnreadable.UnresolvedOptional);

        // Test with Unsupported
        var obsUnsupported = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan"),
            CreateObs("FICTIONAL_FINANCIAL_CAPACITY", ProposalObservationState.Unsupported, binding, evidenceReference: "Custom format", explanation: "Unsupported optional format")
        };

        var resultUnsupported = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obsUnsupported);
        Assert.Equal(ProposalContentCompletenessOutcome.Complete, resultUnsupported.Outcome);
        Assert.Empty(resultUnsupported.ReviewRequiredMandatory);
        Assert.Single(resultUnsupported.UnresolvedOptional);
    }

    // ==========================================
    // SECTION 6: RESULT CONSISTENCY INVARIANTS
    // ==========================================

    [Fact]
    public void Test88_ResultConsistency_CompleteWithMissingMandatory_ThrowsException()
    {
        var template = CreateValidTemplate();
        var snapshot = template.CreateSnapshot();
        var binding = CreateValidBinding();

        var missingReq = template.Requirements.First(r => r.IsMandatory);

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Complete,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: new[] { missingReq },
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" }
            ));

        Assert.Contains("Complete outcome cannot contain missing mandatory requirements", ex.Message);
    }

    [Fact]
    public void Test89_ResultConsistency_CompleteWithReviewRequiredMandatory_ThrowsException()
    {
        var template = CreateValidTemplate();
        var snapshot = template.CreateSnapshot();
        var binding = CreateValidBinding();

        var reviewReq = template.Requirements.First(r => r.IsMandatory);

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Complete,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: new[] { reviewReq },
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" }
            ));

        Assert.Contains("Complete outcome cannot contain review-required mandatory requirements", ex.Message);
    }

    [Fact]
    public void Test90_ResultConsistency_IncompleteWithNoMissingMandatory_ThrowsException()
    {
        var template = CreateValidTemplate();
        var snapshot = template.CreateSnapshot();
        var binding = CreateValidBinding();

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Incomplete,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" }
            ));

        Assert.Contains("Incomplete outcome must contain at least one missing mandatory requirement", ex.Message);
    }

    [Fact]
    public void Test91_ResultConsistency_HumanReviewRequiredWithNoMandatoryReviewReason_ThrowsException()
    {
        var template = CreateValidTemplate(status: ProposalTemplateStatus.Active);
        var snapshot = template.CreateSnapshot();
        var binding = CreateValidBinding();

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.HumanReviewRequired,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" }
            ));

        Assert.Contains("HumanReviewRequired outcome must have at least one mandatory review-required item", ex.Message);
    }

    [Fact]
    public void Test92_ResultConsistency_ConfirmedResultWithoutReviewerOrTimestamp_ThrowsException()
    {
        var template = CreateValidTemplate();
        var snapshot = template.CreateSnapshot();
        var binding = CreateValidBinding();

        // 1. Missing actor
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Complete,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" },
                isConfirmed: true,
                confirmedByActorId: null,
                confirmedAtUtc: _utcNow
            ));

        // 2. Missing timestamp
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Complete,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" },
                isConfirmed: true,
                confirmedByActorId: _officerId,
                confirmedAtUtc: null
            ));
    }

    [Fact]
    public void Test93_ResultConsistency_CorrectedResultWithoutSupersededLinkReasonOrEvidence_ThrowsException()
    {
        var template = CreateValidTemplate();
        var snapshot = template.CreateSnapshot();
        var binding = CreateValidBinding();
        var priorId = ProposalContentAssessmentResultId.New();

        // Missing reason
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Complete,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" },
                supersedesResultId: priorId,
                correctionReason: "",
                correctionEvidenceReference: "EVID"
            ));

        // Missing evidence
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Complete,
                satisfiedMandatory: Array.Empty<ProposalContentRequirement>(),
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" },
                supersedesResultId: priorId,
                correctionReason: "Reason",
                correctionEvidenceReference: ""
            ));
    }

    [Fact]
    public void Test94_ResultConsistency_RequirementAbsentFromTemplateSnapshot_ThrowsException()
    {
        var template = CreateValidTemplate();
        var snapshot = template.CreateSnapshot();
        var binding = CreateValidBinding();

        var foreignReq = new ProposalContentRequirement(
            new ProposalRequirementId("FOREIGN-REQ"),
            ProposalRequirementKind.Section,
            "Foreign Req",
            true,
            _templateId,
            TemplateVersion,
            "REF",
            1
        );

        var ex = Assert.Throws<InvalidProposalContentAssessmentException>(() =>
            new ProposalContentCompletenessResult(
                ProposalContentAssessmentResultId.New(),
                _leaseCaseId,
                binding,
                snapshot,
                ProposalContentCompletenessOutcome.Complete,
                satisfiedMandatory: new[] { foreignReq },
                missingMandatory: Array.Empty<ProposalContentRequirement>(),
                reviewRequiredMandatory: Array.Empty<ProposalContentRequirement>(),
                missingOptional: Array.Empty<ProposalContentRequirement>(),
                satisfiedOptional: Array.Empty<ProposalContentRequirement>(),
                explanations: new[] { "Note" }
            ));

        Assert.Contains("is not present in template snapshot", ex.Message);
    }

    // ==========================================
    // SECTION 7: SEQUENCE ALLOCATION INVARIANTS
    // ==========================================

    [Fact]
    public void Test95_SequenceAllocation_CallerProvidedSequenceNumber_IsOverriddenByAggregate()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        // Caller attempts to inject sequence number 9999
        var result1 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        var manipulatedResult1 = result1.WithSequenceNumber(9999);
        Assert.Equal(9999, manipulatedResult1.SequenceNumber);

        leaseCase.RecordProposalContentAssessment(manipulatedResult1);

        // Aggregate must assign sequence number 1, overriding 9999
        var recorded1 = leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(manipulatedResult1.Id));
        Assert.Equal(1, recorded1.SequenceNumber);

        // Second result with caller injecting 500
        var result2 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        var manipulatedResult2 = result2.WithSequenceNumber(500);
        leaseCase.RecordProposalContentAssessment(manipulatedResult2);

        var recorded2 = leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(manipulatedResult2.Id));
        Assert.Equal(2, recorded2.SequenceNumber);

        // Verify strictly increasing and unique
        var sequences = leaseCase.ProposalContentAssessmentHistory.Select(a => a.SequenceNumber).ToList();
        Assert.Equal(new[] { 1, 2 }, sequences);
    }

    // =========================================================================
    // SECTION 8: MANDATORY TEMPLATE-DEFINITION DIGEST MATCHING (BATCH 3L SAFEGUARD)
    // =========================================================================

    // 96. Null, empty, or whitespace digest is rejected
    [Fact]
    public void Test96_CurrentAssessment_NullOrEmptyDigest_ThrowsException()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        // GetCurrentProposalContentAssessment with null/empty/whitespace digest
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.GetCurrentProposalContentAssessment(binding, expectedTemplateDigest: null!));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.GetCurrentProposalContentAssessment(binding, expectedTemplateDigest: ""));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.GetCurrentProposalContentAssessment(binding, expectedTemplateDigest: "   "));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.GetCurrentProposalContentAssessment(binding, currentTemplateSnapshot: (ProposalTemplateSnapshot)null!));

        // IsAssessmentCurrent with null/empty/whitespace digest
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, binding, expectedTemplateDigest: null!));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, binding, expectedTemplateDigest: ""));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, binding, expectedTemplateDigest: "   "));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, binding, currentTemplateSnapshot: (ProposalTemplateSnapshot)null!));

        // IsAssessmentCurrent coordinate overload with null/empty/whitespace digest
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, TemplateVersion, expectedTemplateDigest: null!));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, TemplateVersion, expectedTemplateDigest: ""));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, TemplateVersion, expectedTemplateDigest: "   "));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            leaseCase.IsAssessmentCurrent(result.Id, _versionId, _checksum, _templateId, TemplateVersion, currentTemplateSnapshot: (ProposalTemplateSnapshot)null!));

        // ProposalContentCompletenessResult.IsCurrentFor with null/empty/whitespace digest
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(binding, expectedTemplateDigest: null!));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(binding, expectedTemplateDigest: ""));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(binding, expectedTemplateDigest: "   "));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(binding, currentTemplateSnapshot: (ProposalTemplateSnapshot)null!));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(_versionId, _checksum, _templateId, TemplateVersion, expectedTemplateDigest: null!));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(_versionId, _checksum, _templateId, TemplateVersion, expectedTemplateDigest: ""));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(_versionId, _checksum, _templateId, TemplateVersion, expectedTemplateDigest: "   "));
        Assert.Throws<InvalidProposalTemplateException>(() =>
            result.IsCurrentFor(_versionId, _checksum, _templateId, TemplateVersion, currentTemplateSnapshot: (ProposalTemplateSnapshot)null!));
    }

    // 97. Same template ID and version with different definition digest does not match
    [Fact]
    public void Test97_CurrentAssessment_SameTemplateIdAndVersion_DifferentDigest_DoesNotMatch()
    {
        var leaseCase = CreateLeaseCase();
        var binding = CreateValidBinding();

        // Template Definition A: has 3 requirements
        var templateA = CreateValidTemplate(version: "2026.1");
        var digestA = templateA.CreateSnapshot().DefinitionDigest;

        // Template Definition B: has same template ID and version "2026.1", but different requirements
        var reqsB = new List<ProposalContentRequirement>
        {
            new(
                new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"),
                ProposalRequirementKind.Section,
                "Fictional Project Description",
                isMandatory: true,
                _templateId,
                "2026.1",
                "FICTIONAL-POLICY-REF-01",
                orderIndex: 1
            ),
            new(
                new ProposalRequirementId("FICTIONAL_LAND_REQUIREMENT"),
                ProposalRequirementKind.Field,
                "Fictional Land Requirement",
                isMandatory: true,
                _templateId,
                "2026.1",
                "FICTIONAL-POLICY-REF-02",
                orderIndex: 2
            ),
            new(
                new ProposalRequirementId("FICTIONAL_ENVIRONMENTAL_STUDY"),
                ProposalRequirementKind.Section,
                "Fictional Environmental Study",
                isMandatory: true,
                _templateId,
                "2026.1",
                "FICTIONAL-POLICY-REF-03",
                orderIndex: 3
            )
        };
        var templateB = new ProposalTemplate(
            _templateId,
            "2026.1",
            "Template Definition B",
            "POLICY-DEF-B",
            ProposalTemplateStatus.Active,
            reqsB
        );
        var digestB = templateB.CreateSnapshot().DefinitionDigest;

        Assert.NotEqual(digestA, digestB);

        var obsA = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var resultA = ProposalContentCompletenessEvaluator.Evaluate(binding, templateA, obsA);
        leaseCase.RecordProposalContentAssessment(resultA);

        // When queried with expectedTemplateDigest for Template B, resultA must NOT match even though ID and Version are identical
        var currentForB = leaseCase.GetCurrentProposalContentAssessment(binding, digestB);
        Assert.Null(currentForB);

        // IsAssessmentCurrent returns false for template B digest
        Assert.False(leaseCase.IsAssessmentCurrent(resultA.Id, binding, digestB));
        Assert.False(leaseCase.IsAssessmentCurrent(resultA.Id, _versionId, _checksum, _templateId, "2026.1", digestB));
        Assert.False(resultA.IsCurrentFor(binding, digestB));
        Assert.False(resultA.IsCurrentFor(_versionId, _checksum, _templateId, "2026.1", digestB));

        // When queried with snapshot B
        Assert.Null(leaseCase.GetCurrentProposalContentAssessment(binding, templateB.CreateSnapshot()));
        Assert.False(leaseCase.IsAssessmentCurrent(resultA.Id, binding, templateB.CreateSnapshot()));
    }

    // 98. Exact binding and exact digest selects the correct result
    [Fact]
    public void Test98_CurrentAssessment_ExactBindingAndExactDigest_SelectsCorrectResult()
    {
        var leaseCase = CreateLeaseCase();
        var binding = CreateValidBinding();

        var templateA = CreateValidTemplate(version: "2026.1");
        var snapshotA = templateA.CreateSnapshot();

        var obsA = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var resultA = ProposalContentCompletenessEvaluator.Evaluate(binding, templateA, obsA);
        leaseCase.RecordProposalContentAssessment(resultA);

        // Exact binding and exact digest returns resultA
        var current = leaseCase.GetCurrentProposalContentAssessment(binding, snapshotA.DefinitionDigest);
        Assert.NotNull(current);
        Assert.Equal(resultA.Id, current.Id);
        Assert.Equal(snapshotA.DefinitionDigest, current.TemplateSnapshot.DefinitionDigest);

        // Using snapshot overload
        var currentFromSnapshot = leaseCase.GetCurrentProposalContentAssessment(binding, snapshotA);
        Assert.NotNull(currentFromSnapshot);
        Assert.Equal(resultA.Id, currentFromSnapshot.Id);

        // IsAssessmentCurrent validates correctly
        Assert.True(leaseCase.IsAssessmentCurrent(resultA.Id, binding, snapshotA.DefinitionDigest));
        Assert.True(leaseCase.IsAssessmentCurrent(resultA.Id, binding, snapshotA));
        Assert.True(leaseCase.IsAssessmentCurrent(resultA.Id, _versionId, _checksum, _templateId, "2026.1", snapshotA.DefinitionDigest));
        Assert.True(leaseCase.IsAssessmentCurrent(resultA.Id, _versionId, _checksum, _templateId, "2026.1", snapshotA));
        Assert.True(resultA.IsCurrentFor(binding, snapshotA.DefinitionDigest));
        Assert.True(resultA.IsCurrentFor(binding, snapshotA));
    }

    // 99. Late confirmation of an older template definition cannot become current for a newer definition with the same ID/version
    [Fact]
    public void Test99_LateConfirmationOfOlderTemplateDefinition_CannotBecomeCurrentForNewerDefinitionWithSameIdAndVersion()
    {
        var leaseCase = CreateLeaseCase();
        var binding = CreateValidBinding();

        // 1. Definition A with ID "FICTIONAL-PROPOSAL-TEMPLATE-01" and Version "2026.1"
        var templateA = CreateValidTemplate(version: "2026.1");
        var digestA = templateA.CreateSnapshot().DefinitionDigest;

        var obsA = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc A"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land A"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan A")
        };
        var resultA = ProposalContentCompletenessEvaluator.Evaluate(binding, templateA, obsA);
        leaseCase.RecordProposalContentAssessment(resultA);
        Assert.Equal(1, leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(resultA.Id)).SequenceNumber);

        // 2. Definition B with SAME ID and Version "2026.1", but amended requirements
        var reqsB = new List<ProposalContentRequirement>
        {
            new(
                new ProposalRequirementId("FICTIONAL_PROJECT_DESCRIPTION"),
                ProposalRequirementKind.Section,
                "Fictional Project Description",
                isMandatory: true,
                _templateId,
                "2026.1",
                "FICTIONAL-POLICY-REF-01",
                orderIndex: 1
            ),
            new(
                new ProposalRequirementId("FICTIONAL_LAND_REQUIREMENT"),
                ProposalRequirementKind.Field,
                "Fictional Land Requirement",
                isMandatory: true,
                _templateId,
                "2026.1",
                "FICTIONAL-POLICY-REF-02",
                orderIndex: 2
            ),
            new(
                new ProposalRequirementId("FICTIONAL_ENVIRONMENTAL_STUDY"),
                ProposalRequirementKind.Section,
                "Fictional Environmental Study",
                isMandatory: true,
                _templateId,
                "2026.1",
                "FICTIONAL-POLICY-REF-03",
                orderIndex: 3
            )
        };
        var templateB = new ProposalTemplate(
            _templateId,
            "2026.1",
            "Template Definition B",
            "POLICY-DEF-B",
            ProposalTemplateStatus.Active,
            reqsB
        );
        var digestB = templateB.CreateSnapshot().DefinitionDigest;
        Assert.NotEqual(digestA, digestB);

        var obsB = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc B"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land B"),
            CreateObs("FICTIONAL_ENVIRONMENTAL_STUDY", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Study B")
        };
        var resultB = ProposalContentCompletenessEvaluator.Evaluate(binding, templateB, obsB);
        leaseCase.RecordProposalContentAssessment(resultB);
        Assert.Equal(2, leaseCase.ProposalContentAssessmentHistory.First(a => a.Id.Equals(resultB.Id)).SequenceNumber);

        // 3. Officer later confirms older Result A (evaluated under Definition A)
        var lateTime = _utcNow.AddHours(4);
        var authority = CreateAuthoritySnapshot(_officerId, actionTime: lateTime);
        var confirmedA = leaseCase.ConfirmProposalContentAssessment(
            resultA.Id,
            authority,
            _officerId,
            lateTime,
            "Late confirmation of older Definition A evaluation"
        );
        Assert.Equal(3, confirmedA.SequenceNumber);

        // Historical query returns confirmedA (highest sequence number in history)
        var latestRecorded = leaseCase.GetLatestRecordedProposalContentAssessment();
        Assert.NotNull(latestRecorded);
        Assert.Equal(confirmedA.Id, latestRecorded.Id);

        // BUT when querying current assessment for Definition B, confirmedA CANNOT become current!
        // It must return resultB which matches digestB!
        var currentForB = leaseCase.GetCurrentProposalContentAssessment(binding, digestB);
        Assert.NotNull(currentForB);
        Assert.Equal(resultB.Id, currentForB.Id);
        Assert.Equal(digestB, currentForB.TemplateSnapshot.DefinitionDigest);

        // And confirmedA is NOT current for Definition B
        Assert.False(leaseCase.IsAssessmentCurrent(confirmedA.Id, binding, digestB));
        Assert.False(leaseCase.IsAssessmentCurrent(confirmedA.Id, _versionId, _checksum, _templateId, "2026.1", digestB));

        // Meanwhile resultB IS current for Definition B
        Assert.True(leaseCase.IsAssessmentCurrent(resultB.Id, binding, digestB));
        Assert.True(leaseCase.IsAssessmentCurrent(resultB.Id, _versionId, _checksum, _templateId, "2026.1", digestB));
    }

    // =========================================================================
    // SECTION 9: EXCEPTION SAFETY AND STATE PRESERVATION (BATCH 3L SAFEGUARD)
    // =========================================================================

    // 100. Rejected confirmation with non-UTC timestamp leaves state unchanged, subsequent valid confirmation gets next sequence
    [Fact]
    public void Test100_RejectedConfirmation_NonUtcTimestamp_LeavesStateUnchanged_AndSubsequentValidConfirmationReceivesNextSequence()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var initialHistoryCount = leaseCase.ProposalContentAssessmentHistory.Count;
        var initialEventCount = leaseCase.DomainEvents.Count;
        var initialRevision = leaseCase.Revision;
        var initialLastSequence = leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber;

        Assert.Equal(1, initialHistoryCount);
        Assert.Equal(1, initialLastSequence);

        var authority = CreateAuthoritySnapshot(_officerId);
        var nonUtcTime = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Local);

        // Attempt rejected confirmation with non-UTC timestamp
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.ConfirmProposalContentAssessment(result.Id, authority, _officerId, nonUtcTime, "Invalid time"));

        // Verify aggregate state was NOT modified
        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // Subsequent valid confirmation must receive next consecutive sequence number (2)
        var validTime = _utcNow.AddHours(1);
        var confirmed = leaseCase.ConfirmProposalContentAssessment(result.Id, authority, _officerId, validTime, "Valid confirmation");

        Assert.Equal(2, confirmed.SequenceNumber);
        Assert.Equal(initialHistoryCount + 1, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount + 1, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.True(confirmed.IsConfirmed);
    }

    // 101. Rejected correction with missing correction evidence leaves state unchanged, subsequent valid correction gets next sequence
    [Fact]
    public void Test101_RejectedCorrection_MissingCorrectionEvidence_LeavesStateUnchanged_AndSubsequentValidCorrectionReceivesNextSequence()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var initialHistoryCount = leaseCase.ProposalContentAssessmentHistory.Count;
        var initialEventCount = leaseCase.DomainEvents.Count;
        var initialRevision = leaseCase.Revision;
        var initialLastSequence = leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber;

        var validTime = _utcNow.AddHours(1);
        var authority = CreateAuthoritySnapshot(_officerId, actionTime: validTime);

        // Rejected corrections with null, empty, or whitespace evidenceReference
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(result.Id, authority, _officerId, validTime, "Valid reason", obs, null!, template));
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(result.Id, authority, _officerId, validTime, "Valid reason", obs, "", template));
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(result.Id, authority, _officerId, validTime, "Valid reason", obs, "   ", template));

        // Verify aggregate state was NOT modified
        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // Subsequent valid correction must succeed and receive next consecutive sequence number (2)
        var corrected = leaseCase.CorrectProposalContentAssessment(
            result.Id,
            authority,
            _officerId,
            validTime,
            "Valid reason",
            obs,
            "EVIDENCE-REF-001",
            template
        );

        Assert.Equal(2, corrected.SequenceNumber);
        Assert.Equal(initialHistoryCount + 1, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount + 1, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal("EVIDENCE-REF-001", corrected.CorrectionEvidenceReference);
    }

    // 102. Rejected correction with non-UTC review timestamp leaves state unchanged, subsequent valid correction gets next sequence
    [Fact]
    public void Test102_RejectedCorrection_NonUtcTimestamp_LeavesStateUnchanged_AndSubsequentValidCorrectionReceivesNextSequence()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var initialHistoryCount = leaseCase.ProposalContentAssessmentHistory.Count;
        var initialEventCount = leaseCase.DomainEvents.Count;
        var initialRevision = leaseCase.Revision;
        var initialLastSequence = leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber;

        var authority = CreateAuthoritySnapshot(_officerId);
        var nonUtcTime = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Unspecified);

        // Attempt rejected correction with non-UTC timestamp
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.CorrectProposalContentAssessment(result.Id, authority, _officerId, nonUtcTime, "Valid reason", obs, "EVIDENCE-REF-001", template));

        // Verify aggregate state was NOT modified
        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // Subsequent valid correction receives next consecutive sequence number (2)
        var validTime = _utcNow.AddHours(2);
        var validAuthority = CreateAuthoritySnapshot(_officerId, actionTime: validTime);
        var corrected = leaseCase.CorrectProposalContentAssessment(
            result.Id,
            validAuthority,
            _officerId,
            validTime,
            "Valid reason",
            obs,
            "EVIDENCE-REF-001",
            template
        );

        Assert.Equal(2, corrected.SequenceNumber);
        Assert.Equal(initialHistoryCount + 1, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount + 1, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
    }

    // 103. Rejected correction with invalid corrected observation leaves state unchanged, subsequent valid correction gets next sequence
    [Fact]
    public void Test103_RejectedCorrection_InvalidCorrectedObservation_LeavesStateUnchanged_AndSubsequentValidCorrectionReceivesNextSequence()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var result = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result);

        var initialHistoryCount = leaseCase.ProposalContentAssessmentHistory.Count;
        var initialEventCount = leaseCase.DomainEvents.Count;
        var initialRevision = leaseCase.Revision;
        var initialLastSequence = leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber;

        var validTime = _utcNow.AddHours(1);
        var authority = CreateAuthoritySnapshot(_officerId, actionTime: validTime);

        // Observation with mismatched DocumentVersionId
        var mismatchedVersionId = new DocumentVersionId(Guid.NewGuid());
        var mismatchedBinding = CreateValidBinding(versionId: mismatchedVersionId);
        var invalidObs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, mismatchedBinding, pageNumber: 1, textSpan: "Desc")
        };

        // Attempt correction with invalid observations (throws InvalidProposalObservationException)
        Assert.Throws<InvalidProposalObservationException>(() =>
            leaseCase.CorrectProposalContentAssessment(result.Id, authority, _officerId, validTime, "Correction reason", invalidObs, "EVIDENCE-REF-001", template));

        // Attempt correction with null observation collection
        Assert.Throws<InvalidProposalObservationException>(() =>
            leaseCase.CorrectProposalContentAssessment(result.Id, authority, _officerId, validTime, "Correction reason", null!, "EVIDENCE-REF-001", template));

        // Verify aggregate state was NOT modified
        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // Subsequent valid correction receives next consecutive sequence number (2)
        var corrected = leaseCase.CorrectProposalContentAssessment(
            result.Id,
            authority,
            _officerId,
            validTime,
            "Valid correction",
            obs,
            "EVIDENCE-REF-001",
            template
        );

        Assert.Equal(2, corrected.SequenceNumber);
        Assert.Equal(initialHistoryCount + 1, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount + 1, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
    }

    // =========================================================================
    // SECTION 10: AUTHORITY BYPASS SAFEGUARDS IN PROPOSAL-CONTENT RECORDING
    // =========================================================================

    // 104. Externally constructed confirmed result is rejected by RecordProposalContentAssessment
    [Fact]
    public void Test104_RecordProposalContentAssessment_ExternallyConstructedConfirmedResult_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var snapshot = template.CreateSnapshot();
        var satisfied = template.Requirements.Where(r => r.IsMandatory).ToList();
        var satisfiedOpt = template.Requirements.Where(r => !r.IsMandatory).ToList();
        var explanations = new[] { "External test assessment" };

        // 1. Result with IsConfirmed == true, ConfirmedByActorId, and ConfirmedAtUtc
        var confirmedResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: true,
            confirmedByActorId: _officerId,
            confirmedAtUtc: _utcNow
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(confirmedResult));

        // 2. Result with WithConfirmation(...)
        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };
        var evalResult = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        var withConfResult = evalResult.WithConfirmation(
            ProposalContentAssessmentResultId.New(),
            _officerId,
            _utcNow,
            "Bypass confirmation attempt",
            0
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(withConfResult));

        // 3. Result with confirmation actor only
        var actorResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            confirmedByActorId: _officerId
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(actorResult));

        // 4. Result with confirmation timestamp only
        var timestampResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            confirmedAtUtc: _utcNow
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(timestampResult));

        // 5. Result with confirmation notes only
        var notesResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            confirmationNotes: "Pre-populated unreviewed notes"
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(notesResult));

        // 6. Result with ConfirmedResultOfId
        var confirmedOfResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            confirmedResultOfId: ProposalContentAssessmentResultId.New()
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(confirmedOfResult));
    }

    // 105. Externally constructed corrected result is rejected by RecordProposalContentAssessment
    [Fact]
    public void Test105_RecordProposalContentAssessment_ExternallyConstructedCorrectedResult_IsRejected()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();
        var snapshot = template.CreateSnapshot();
        var satisfied = template.Requirements.Where(r => r.IsMandatory).ToList();
        var satisfiedOpt = template.Requirements.Where(r => !r.IsMandatory).ToList();
        var explanations = new[] { "External test assessment" };
        var priorResultId = ProposalContentAssessmentResultId.New();

        // 1. Result with SupersedesResultId, CorrectionReason, and CorrectionEvidenceReference
        var correctedResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            supersedesResultId: priorResultId,
            correctionReason: "Bypass correction reason",
            correctionEvidenceReference: "DOC-EVIDENCE-001"
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(correctedResult));

        // 2. Result with correction reason only
        var reasonResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            correctionReason: "Pre-populated correction reason"
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(reasonResult));

        // 3. Result with correction evidence reference only
        var evidenceResult = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            correctionEvidenceReference: "DOC-EVIDENCE-002"
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(evidenceResult));
    }

    // 106. Rejection leaves history, events, Revision and sequence allocation unchanged
    [Fact]
    public void Test106_RecordProposalContentAssessment_RejectionLeavesHistoryEventsRevisionAndSequenceUnchanged()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        // Record initial valid proposed result (sequence 1)
        var result1 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result1);

        var initialHistoryCount = leaseCase.ProposalContentAssessmentHistory.Count;
        var initialEventCount = leaseCase.DomainEvents.Count;
        var initialRevision = leaseCase.Revision;
        var initialLastSequence = leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber;

        Assert.Equal(1, initialHistoryCount);
        Assert.Equal(1, initialLastSequence);

        var snapshot = template.CreateSnapshot();
        var satisfied = template.Requirements.Where(r => r.IsMandatory).ToList();
        var satisfiedOpt = template.Requirements.Where(r => !r.IsMandatory).ToList();
        var explanations = new[] { "External test assessment" };

        // 1. Rejected externally constructed confirmed result
        var confirmedAttempt = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: true,
            confirmedByActorId: _officerId,
            confirmedAtUtc: _utcNow
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(confirmedAttempt));

        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // 2. Rejected externally constructed corrected result
        var correctedAttempt = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            supersedesResultId: result1.Id,
            correctionReason: "Bypass reason",
            correctionEvidenceReference: "EVIDENCE-01"
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(correctedAttempt));

        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // 3. Rejected result with unreviewed notes
        var notesAttempt = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            confirmationNotes: "Notes attempt"
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(notesAttempt));

        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // 4. Rejected result with correction evidence
        var evidenceAttempt = new ProposalContentCompletenessResult(
            ProposalContentAssessmentResultId.New(),
            leaseCase.Id,
            binding,
            snapshot,
            ProposalContentCompletenessOutcome.Complete,
            satisfied,
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            Array.Empty<ProposalContentRequirement>(),
            satisfiedOpt,
            explanations,
            isConfirmed: false,
            correctionEvidenceReference: "DOC-EVIDENCE-ATTEMPT"
        );
        Assert.Throws<InvalidProposalContentReviewException>(() =>
            leaseCase.RecordProposalContentAssessment(evidenceAttempt));

        Assert.Equal(initialHistoryCount, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(initialLastSequence, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // 5. Subsequent valid proposed result recording succeeds and gets sequence 2
        var result2 = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(result2);

        Assert.Equal(initialHistoryCount + 1, leaseCase.ProposalContentAssessmentHistory.Count);
        Assert.Equal(initialEventCount + 1, leaseCase.DomainEvents.Count);
        Assert.Equal(initialRevision, leaseCase.Revision);
        Assert.Equal(2, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);
    }

    // 107. Ordinary proposed-result recording still succeeds
    [Fact]
    public void Test107_RecordProposalContentAssessment_OrdinaryProposedResultRecording_Succeeds()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        var proposedResult = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);

        // Invariants of proposed result before recording
        Assert.False(proposedResult.IsConfirmed);
        Assert.Null(proposedResult.ConfirmedByActorId);
        Assert.Null(proposedResult.ConfirmedAtUtc);
        Assert.Null(proposedResult.ConfirmationNotes);
        Assert.Null(proposedResult.ConfirmedResultOfId);
        Assert.Null(proposedResult.SupersedesResultId);
        Assert.Null(proposedResult.CorrectionReason);
        Assert.Null(proposedResult.CorrectionEvidenceReference);

        // Recording ordinary proposed result succeeds
        leaseCase.RecordProposalContentAssessment(proposedResult);

        Assert.Single(leaseCase.ProposalContentAssessmentHistory);
        var recorded = leaseCase.ProposalContentAssessmentHistory.First();
        Assert.Equal(proposedResult.Id, recorded.Id);
        Assert.Equal(1, recorded.SequenceNumber);
        Assert.False(recorded.IsConfirmed);
        Assert.Null(recorded.ConfirmedByActorId);

        // Emits ProposalContentCompletenessAssessed event
        var domainEvent = Assert.Single(leaseCase.DomainEvents.OfType<ProposalContentCompletenessAssessed>());
        Assert.Equal(leaseCase.Id, domainEvent.LeaseCaseId);
        Assert.Equal(proposedResult.Id, domainEvent.ResultId);
    }

    // 108. Authorised confirmation and correction still succeed
    [Fact]
    public void Test108_AuthorisedConfirmationAndCorrection_EnterHistoryAndSucceed()
    {
        var leaseCase = CreateLeaseCase();
        var template = CreateValidTemplate();
        var binding = CreateValidBinding();

        var obs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding, pageNumber: 2, textSpan: "Land"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding, pageNumber: 3, textSpan: "Plan")
        };

        // 1. Record ordinary proposed result (sequence 1)
        var proposedResult = ProposalContentCompletenessEvaluator.Evaluate(binding, template, obs);
        leaseCase.RecordProposalContentAssessment(proposedResult);
        Assert.Equal(1, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        var confirmTime = _utcNow.AddHours(1);
        var authority = CreateAuthoritySnapshot(_officerId, actionTime: confirmTime);

        // 2. Authorised confirmation succeeds (sequence 2)
        var confirmed = leaseCase.ConfirmProposalContentAssessment(
            proposedResult.Id,
            authority,
            _officerId,
            confirmTime,
            "Officer verified completeness"
        );

        Assert.NotNull(confirmed);
        Assert.True(confirmed.IsConfirmed);
        Assert.Equal(_officerId, confirmed.ConfirmedByActorId);
        Assert.Equal(confirmTime, confirmed.ConfirmedAtUtc);
        Assert.Equal("Officer verified completeness", confirmed.ConfirmationNotes);
        Assert.Equal(proposedResult.Id, confirmed.ConfirmedResultOfId);
        Assert.Equal(2, confirmed.SequenceNumber);
        Assert.Equal(2, leaseCase.ProposalContentAssessmentHistory.Count);

        var confirmEvent = Assert.Single(leaseCase.DomainEvents.OfType<ProposalContentAssessmentConfirmed>());
        Assert.Equal(confirmed.Id, confirmEvent.ResultId);
        Assert.Equal(_officerId, confirmEvent.ConfirmedByActorId);

        // 3. Record a second proposed result to be corrected (sequence 3)
        var binding2 = CreateValidBinding(checksum: new DocumentChecksum("SHA-256", "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899"));
        var obs2 = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding2, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Missing, binding2, pageNumber: 2, explanation: "Missing"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding2, pageNumber: 3, textSpan: "Plan")
        };
        var proposedResult2 = ProposalContentCompletenessEvaluator.Evaluate(binding2, template, obs2);
        leaseCase.RecordProposalContentAssessment(proposedResult2);
        Assert.Equal(3, leaseCase.ProposalContentAssessmentHistory.Last().SequenceNumber);

        // 4. Authorised correction succeeds (sequence 4)
        var correctTime = _utcNow.AddHours(2);
        var correctAuthority = CreateAuthoritySnapshot(_officerId, actionTime: correctTime);
        var correctedObs = new List<ProposalRequirementObservation>
        {
            CreateObs("FICTIONAL_PROJECT_DESCRIPTION", ProposalObservationState.Present, binding2, pageNumber: 1, textSpan: "Desc"),
            CreateObs("FICTIONAL_LAND_REQUIREMENT", ProposalObservationState.Present, binding2, pageNumber: 2, textSpan: "Land now present"),
            CreateObs("FICTIONAL_IMPLEMENTATION_PLAN", ProposalObservationState.Present, binding2, pageNumber: 3, textSpan: "Plan")
        };

        var corrected = leaseCase.CorrectProposalContentAssessment(
            proposedResult2.Id,
            correctAuthority,
            _officerId,
            correctTime,
            "Officer corrected missing land requirement",
            correctedObs,
            "EVIDENCE-CORRECTION-RECORD-001",
            template
        );

        Assert.NotNull(corrected);
        Assert.True(corrected.IsConfirmed);
        Assert.Equal(_officerId, corrected.ConfirmedByActorId);
        Assert.Equal(correctTime, corrected.ConfirmedAtUtc);
        Assert.Equal("Officer corrected missing land requirement", corrected.CorrectionReason);
        Assert.Equal("EVIDENCE-CORRECTION-RECORD-001", corrected.CorrectionEvidenceReference);
        Assert.Equal(proposedResult2.Id, corrected.SupersedesResultId);
        Assert.Equal(4, corrected.SequenceNumber);
        Assert.Equal(4, leaseCase.ProposalContentAssessmentHistory.Count);

        var correctEvent = Assert.Single(leaseCase.DomainEvents.OfType<ProposalContentAssessmentCorrected>());
        Assert.Equal(corrected.Id, correctEvent.CorrectedResultId);
        Assert.Equal(proposedResult2.Id, correctEvent.OriginalResultId);

        // Verify sequence numbers across aggregate history are strictly [1, 2, 3, 4]
        var allSequences = leaseCase.ProposalContentAssessmentHistory.Select(a => a.SequenceNumber).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4 }, allSequences);
    }
}

