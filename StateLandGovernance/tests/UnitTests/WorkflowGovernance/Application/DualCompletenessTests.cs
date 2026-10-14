namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public class DualCompletenessTests
{
    private const string Sha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private readonly SpyDocumentCompletenessAssessmentRepository _completenessRepository;
    private readonly SpyLeaseCaseRepository _leaseCaseRepository;
    private readonly FakeDocumentRequirementProvider _requirementProvider;
    private readonly FakeProposalTemplateProvider _templateProvider;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;

    private readonly AssessDocumentCompletenessCommandHandler _docAssessHandler;
    private readonly AssessProposalContentCommandHandler _propAssessHandler;
    private readonly CorrectProposalContentAssessmentCommandHandler _propCorrectHandler;
    private readonly GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler _getLatestDocHandler;
    private readonly GetCurrentProposalContentAssessmentQueryHandler _getCurrentPropHandler;

    public DualCompletenessTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
        _completenessRepository = new SpyDocumentCompletenessAssessmentRepository();
        _leaseCaseRepository = new SpyLeaseCaseRepository();
        _requirementProvider = new FakeDocumentRequirementProvider();
        _templateProvider = new FakeProposalTemplateProvider();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _docAssessHandler = new AssessDocumentCompletenessCommandHandler(
            _completenessRepository,
            _leaseCaseRepository,
            _requirementProvider,
            _unitOfWork,
            new AssessDocumentCompletenessCommandValidator(),
            _timeProvider);

        _propAssessHandler = new AssessProposalContentCommandHandler(
            _leaseCaseRepository,
            _templateProvider,
            _unitOfWork,
            new AssessProposalContentCommandValidator(),
            _timeProvider);

        _propCorrectHandler = new CorrectProposalContentAssessmentCommandHandler(
            _leaseCaseRepository,
            _templateProvider,
            _unitOfWork,
            new CorrectProposalContentAssessmentCommandValidator(),
            _timeProvider);

        _getLatestDocHandler = new GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler(_completenessRepository);
        _getCurrentPropHandler = new GetCurrentProposalContentAssessmentQueryHandler(_leaseCaseRepository);
    }

    private LeaseCase CreateCase(Guid caseId)
    {
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-50),
            _fixedUtcTime.AddHours(2));

        var leaseCase = new LeaseCase(
            new LeaseCaseId(caseId),
            "APP-2026-0001",
            actorId,
            _fixedUtcTime.AddHours(-2),
            authority);

        _leaseCaseRepository.Seed(leaseCase);
        return leaseCase;
    }

    private AssessedDocumentBindingDto CreateBinding(Guid docId, Guid versionId)
    {
        return new AssessedDocumentBindingDto(
            GovernedDocumentId: docId,
            DocumentVersionId: versionId,
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1);
    }

    private ProposalTemplate CreateStandardTemplate()
    {
        var templateId = new ProposalTemplateId("LEASE_TEMPLATE");
        var version = "v1.0";

        var requirements = new List<ProposalContentRequirement>
        {
            new ProposalContentRequirement(
                new ProposalRequirementId("APPLICANT_DETAILS"),
                ProposalRequirementKind.Section,
                "Applicant Details",
                isMandatory: true,
                templateId,
                version,
                "POLICY-01",
                1,
                null),
            new ProposalContentRequirement(
                new ProposalRequirementId("LAND_DETAILS"),
                ProposalRequirementKind.Section,
                "Land Details",
                isMandatory: true,
                templateId,
                version,
                "POLICY-02",
                2,
                null),
            new ProposalContentRequirement(
                new ProposalRequirementId("PURPOSE"),
                ProposalRequirementKind.Section,
                "Development Purpose",
                isMandatory: true,
                templateId,
                version,
                "POLICY-03",
                3,
                null)
        };

        return new ProposalTemplate(
            templateId,
            version,
            "Standard Proposal Template",
            "GOV-GUIDE-2026",
            ProposalTemplateStatus.Active,
            requirements);
    }

    [Fact]
    public async Task DualCompleteness_DocumentCompletenessComplete_And_ProposalContentIncomplete_CanCoexist()
    {
        // Arrange: A single lease case
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);

        // 1. Assess Document Completeness -> All required documents present -> Complete
        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();
        var doc2Id = Guid.NewGuid();
        var ver2Id = Guid.NewGuid();

        var assessedDocs = new List<AssessedDocumentBindingDto>
        {
            CreateBinding(doc1Id, ver1Id),
            CreateBinding(doc2Id, ver2Id)
        };

        var classifiedDocs = new List<ClassifiedDocumentDto>
        {
            new ClassifiedDocumentDto(
                Id: Guid.NewGuid(),
                DocumentBinding: CreateBinding(doc1Id, ver1Id),
                OriginalClassificationCode: "SurveyPlan",
                ConfidenceScore: 0.95m,
                Status: "Accepted",
                ClassifiedAt: _fixedUtcTime.AddMinutes(-10)),

            new ClassifiedDocumentDto(
                Id: Guid.NewGuid(),
                DocumentBinding: CreateBinding(doc2Id, ver2Id),
                OriginalClassificationCode: "IdentityProof",
                ConfidenceScore: 0.98m,
                Status: "Accepted",
                ClassifiedAt: _fixedUtcTime.AddMinutes(-10))
        };

        _requirementProvider.RegisterSet("STANDARD_LEASE_DOCS", "v1.0", new List<DocumentRequirementSnapshot>
        {
            new DocumentRequirementSnapshot(
                new DocumentRequirementId(Guid.NewGuid()),
                new DocumentClassificationCode("SurveyPlan"),
                RequirementCriticality.Mandatory,
                RequirementApplicability.Required,
                1,
                "SURVEY_REQ",
                "Survey plan required"),
            new DocumentRequirementSnapshot(
                new DocumentRequirementId(Guid.NewGuid()),
                new DocumentClassificationCode("IdentityProof"),
                RequirementCriticality.Mandatory,
                RequirementApplicability.Required,
                1,
                "ID_REQ",
                "Identity proof required")
        });

        var docCommand = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE_DOCS",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        var docResult = await _docAssessHandler.HandleAsync(docCommand);
        Assert.Equal("Complete", docResult.Outcome);
        Assert.Empty(docResult.MissingRequirements);

        // 2. Assess Proposal Content on the same case -> Missing mandatory section "PURPOSE" -> Incomplete
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var propObservations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Missing", 3, null, null, null, "Section omitted in draft")
        };

        var propCommand = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: template.Id.Value,
            TemplateVersion: template.Version,
            ExtractionReference: "ext-1",
            Observations: propObservations,
            ExpectedRevision: leaseCase.Revision);

        var propResult = await _propAssessHandler.HandleAsync(propCommand);
        Assert.Equal("Incomplete", propResult.Outcome);
        Assert.Contains(propResult.MissingMandatory, s => s.Id == "PURPOSE");

        // 3. Assert Dual Completeness Invariant:
        // Document completeness is Complete, while Proposal Content is Incomplete on the SAME case
        var latestDoc = await _getLatestDocHandler.HandleAsync(new GetLatestDocumentCompletenessAssessmentByCaseIdQuery(caseId));
        Assert.NotNull(latestDoc);
        Assert.Equal("Complete", latestDoc.Outcome);

        var currentProp = await _getCurrentPropHandler.HandleAsync(new GetCurrentProposalContentAssessmentQuery(caseId, propResult.TemplateSnapshot.DefinitionDigest));
        Assert.NotNull(currentProp);
        Assert.Equal("Incomplete", currentProp.Outcome);

        // Neither derives from or alters the other
        Assert.NotEqual(latestDoc.Outcome, currentProp.Outcome);
    }

    [Fact]
    public async Task DualCompleteness_DocumentCompletenessIncomplete_And_ProposalContentComplete_CanCoexist()
    {
        // Arrange: A single lease case
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);

        // 1. Assess Document Completeness -> Missing required "SurveyPlan" -> MissingRequiredDocuments
        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();

        var assessedDocs = new List<AssessedDocumentBindingDto>
        {
            CreateBinding(doc1Id, ver1Id)
        };

        var classifiedDocs = new List<ClassifiedDocumentDto>
        {
            new ClassifiedDocumentDto(
                Id: Guid.NewGuid(),
                DocumentBinding: CreateBinding(doc1Id, ver1Id),
                OriginalClassificationCode: "IdentityProof",
                ConfidenceScore: 0.98m,
                Status: "Accepted",
                ClassifiedAt: _fixedUtcTime.AddMinutes(-10))
        };

        _requirementProvider.RegisterSet("STANDARD_LEASE_DOCS", "v1.0", new List<DocumentRequirementSnapshot>
        {
            new DocumentRequirementSnapshot(
                new DocumentRequirementId(Guid.NewGuid()),
                new DocumentClassificationCode("SurveyPlan"),
                RequirementCriticality.Mandatory,
                RequirementApplicability.Required,
                1,
                "SURVEY_REQ",
                "Survey plan required"),
            new DocumentRequirementSnapshot(
                new DocumentRequirementId(Guid.NewGuid()),
                new DocumentClassificationCode("IdentityProof"),
                RequirementCriticality.Mandatory,
                RequirementApplicability.Required,
                1,
                "ID_REQ",
                "Identity proof required")
        });

        var docCommand = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE_DOCS",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        var docResult = await _docAssessHandler.HandleAsync(docCommand);
        Assert.Equal("MissingRequiredDocuments", docResult.Outcome);
        Assert.Single(docResult.MissingRequirements);
        Assert.Equal("SurveyPlan", docResult.MissingRequirements[0].RequiredClassificationCode);

        // 2. Assess Proposal Content on the same case -> All mandatory sections present -> Complete
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var propObservations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism", "ev-3", "ext-3", "Found")
        };

        var propCommand = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: template.Id.Value,
            TemplateVersion: template.Version,
            ExtractionReference: "ext-1",
            Observations: propObservations,
            ExpectedRevision: leaseCase.Revision);

        var propResult = await _propAssessHandler.HandleAsync(propCommand);
        Assert.Equal("Complete", propResult.Outcome);
        Assert.NotEmpty(propResult.SatisfiedMandatory);
        Assert.Empty(propResult.MissingMandatory);

        // 3. Assert Dual Completeness Invariant:
        // Document completeness is Incomplete (MissingRequiredDocuments), while Proposal Content is Complete on the SAME case
        var latestDoc = await _getLatestDocHandler.HandleAsync(new GetLatestDocumentCompletenessAssessmentByCaseIdQuery(caseId));
        Assert.NotNull(latestDoc);
        Assert.Equal("MissingRequiredDocuments", latestDoc.Outcome);

        var currentProp = await _getCurrentPropHandler.HandleAsync(new GetCurrentProposalContentAssessmentQuery(caseId, propResult.TemplateSnapshot.DefinitionDigest));
        Assert.NotNull(currentProp);
        Assert.Equal("Complete", currentProp.Outcome);

        // Neither derives from or alters the other
        Assert.NotEqual(latestDoc.Outcome, currentProp.Outcome);
    }

    [Fact]
    public async Task DualCompleteness_CorrectingProposalContent_DoesNotMutateDocumentCompleteness()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);

        // Assess document completeness as MissingRequiredDocuments
        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();
        var assessedDocs = new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) };
        var classifiedDocs = new List<ClassifiedDocumentDto>
        {
            new ClassifiedDocumentDto(
                Id: Guid.NewGuid(),
                DocumentBinding: CreateBinding(doc1Id, ver1Id),
                OriginalClassificationCode: "IdentityProof",
                ConfidenceScore: 0.98m,
                Status: "Accepted",
                ClassifiedAt: _fixedUtcTime.AddMinutes(-10))
        };
        _requirementProvider.RegisterSet("STANDARD_LEASE_DOCS", "v1.0", new List<DocumentRequirementSnapshot>
        {
            new DocumentRequirementSnapshot(
                new DocumentRequirementId(Guid.NewGuid()),
                new DocumentClassificationCode("SurveyPlan"),
                RequirementCriticality.Mandatory,
                RequirementApplicability.Required,
                1,
                "SURVEY_REQ",
                "Survey plan required")
        });

        var docCommand = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE_DOCS",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        await _docAssessHandler.HandleAsync(docCommand);

        // Initial proposal content assessment is Incomplete
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var initialObservations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Missing", 3, null, null, null, "Omitted")
        };

        var initialPropCommand = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: template.Id.Value,
            TemplateVersion: template.Version,
            ExtractionReference: "ext-1",
            Observations: initialObservations,
            ExpectedRevision: leaseCase.Revision);

        var initialPropResult = await _propAssessHandler.HandleAsync(initialPropCommand);
        Assert.Equal("Incomplete", initialPropResult.Outcome);

        // Act: Officer corrects the proposal content assessment to supply the missing section
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "ProposalReviewer" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(3));

        var correctedObservations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism", "ev-3", "ext-3", "Found in appendix")
        };

        var correctCommand = new CorrectProposalContentAssessmentCommand(
            LeaseCaseId: caseId,
            AssessmentResultId: initialPropResult.Id,
            ActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: leaseCase.Revision,
            Reason: "Purpose found in attached schedule B",
            EvidenceReference: "ev-schedule-b",
            CorrectedObservations: correctedObservations);

        var correctedPropResult = await _propCorrectHandler.HandleAsync(correctCommand);
        Assert.Equal("Complete", correctedPropResult.Outcome);

        // Assert: Document completeness remained completely untouched and still MissingRequiredDocuments
        var docAssessment = await _getLatestDocHandler.HandleAsync(new GetLatestDocumentCompletenessAssessmentByCaseIdQuery(caseId));
        Assert.NotNull(docAssessment);
        Assert.Equal("MissingRequiredDocuments", docAssessment.Outcome);
        Assert.Single(docAssessment.MissingRequirements);
    }

    private sealed class FakeDocumentRequirementProvider : IDocumentRequirementProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DocumentRequirementSnapshot>> _sets = new();

        public void RegisterSet(string id, string version, IReadOnlyList<DocumentRequirementSnapshot> requirements)
        {
            _sets[$"{id}:{version}"] = requirements;
        }

        public Task<IReadOnlyList<DocumentRequirementSnapshot>?> GetRequirementSetAsync(
            string requirementSetIdentifier,
            string requirementSetVersion,
            CancellationToken cancellationToken = default)
        {
            _sets.TryGetValue($"{requirementSetIdentifier}:{requirementSetVersion}", out var found);
            return Task.FromResult<IReadOnlyList<DocumentRequirementSnapshot>?>(found);
        }
    }

    private sealed class FakeProposalTemplateProvider : IProposalTemplateProvider
    {
        private readonly Dictionary<string, ProposalTemplate> _templates = new();

        public void RegisterTemplate(ProposalTemplate template)
        {
            _templates[$"{template.Id.Value}:{template.Version}"] = template;
        }

        public Task<ProposalTemplate?> GetTemplateAsync(ProposalTemplateId id, string version, CancellationToken cancellationToken = default)
        {
            _templates.TryGetValue($"{id.Value}:{version}", out var template);
            return Task.FromResult<ProposalTemplate?>(template);
        }
    }

    private sealed class SpyDocumentCompletenessAssessmentRepository : IDocumentCompletenessAssessmentRepository
    {
        private readonly Dictionary<Guid, DocumentCompletenessAssessment> _assessmentsById = new();

        public void Seed(DocumentCompletenessAssessment assessment)
        {
            _assessmentsById[assessment.Id.Value] = assessment;
        }

        public Task<DocumentCompletenessAssessment?> GetByIdAsync(DocumentCompletenessAssessmentId id, CancellationToken cancellationToken = default)
        {
            _assessmentsById.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<IReadOnlyList<DocumentCompletenessAssessment>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            var found = _assessmentsById.Values.Where(a => a.LeaseCaseId.Equals(leaseCaseId)).ToList();
            return Task.FromResult<IReadOnlyList<DocumentCompletenessAssessment>>(found);
        }

        public Task AddAsync(DocumentCompletenessAssessment assessment, CancellationToken cancellationToken = default)
        {
            Seed(assessment);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyLeaseCaseRepository : ILeaseCaseRepository
    {
        private readonly Dictionary<Guid, LeaseCase> _casesById = new();

        public void Seed(LeaseCase leaseCase)
        {
            _casesById[leaseCase.Id.Value] = leaseCase;
        }

        public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        {
            _casesById.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
        {
            var found = _casesById.Values.FirstOrDefault(c => string.Equals(c.ApplicationReference, applicationReference, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found);
        }

        public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
        {
            Seed(leaseCase);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyWorkflowGovernanceUnitOfWork : IWorkflowGovernanceUnitOfWork
    {
        public int CommitCount { get; private set; }

        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
