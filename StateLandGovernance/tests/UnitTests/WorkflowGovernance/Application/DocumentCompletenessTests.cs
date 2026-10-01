namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public class DocumentCompletenessTests
{
    private const string Sha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private readonly SpyDocumentCompletenessAssessmentRepository _completenessRepository;
    private readonly SpyLeaseCaseRepository _leaseCaseRepository;
    private readonly FakeDocumentRequirementProvider _requirementProvider;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;

    private readonly AssessDocumentCompletenessCommandHandler _assessHandler;
    private readonly ReviewDocumentClassificationCommandHandler _reviewHandler;
    private readonly GetDocumentCompletenessAssessmentByIdQueryHandler _getByIdHandler;
    private readonly GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler _getLatestHandler;

    public DocumentCompletenessTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
        _completenessRepository = new SpyDocumentCompletenessAssessmentRepository();
        _leaseCaseRepository = new SpyLeaseCaseRepository();
        _requirementProvider = new FakeDocumentRequirementProvider();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _assessHandler = new AssessDocumentCompletenessCommandHandler(
            _completenessRepository,
            _leaseCaseRepository,
            _requirementProvider,
            _unitOfWork,
            new AssessDocumentCompletenessCommandValidator(),
            _timeProvider);

        _reviewHandler = new ReviewDocumentClassificationCommandHandler(
            _completenessRepository,
            _unitOfWork,
            new ReviewDocumentClassificationCommandValidator(),
            _timeProvider);

        _getByIdHandler = new GetDocumentCompletenessAssessmentByIdQueryHandler(_completenessRepository);
        _getLatestHandler = new GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler(_completenessRepository);
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

    private static DocumentRequirementSnapshot CreateRequirement(
        string code,
        int minCount = 1,
        RequirementCriticality criticality = RequirementCriticality.Mandatory,
        RequirementApplicability applicability = RequirementApplicability.Required,
        string reasonCode = "STANDARD_REQ",
        string description = "Standard requirement")
    {
        return new DocumentRequirementSnapshot(
            new DocumentRequirementId(Guid.NewGuid()),
            new DocumentClassificationCode(code),
            criticality,
            applicability,
            minCount,
            reasonCode,
            description);
    }

    [Fact]
    public async Task AssessDocumentCompleteness_AllRequiredDocumentsPresent_EvaluatesToComplete_CommitsOnce()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

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

        // Register requirements with trusted provider
        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("SurveyPlan"),
            CreateRequirement("IdentityProof")
        });

        var command = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        // Act
        var result = await _assessHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Complete", result.Outcome);
        Assert.Empty(result.MissingRequirements);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AssessDocumentCompleteness_MissingRequiredDocument_EvaluatesToMissingRequiredDocuments_PreservesDeficit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

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

        // Register requirements with trusted provider: SurveyPlan is required but not provided
        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("SurveyPlan"),
            CreateRequirement("IdentityProof")
        });

        var command = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        // Act
        var result = await _assessHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MissingRequiredDocuments", result.Outcome);
        Assert.Single(result.MissingRequirements);
        Assert.Equal("SurveyPlan", result.MissingRequirements[0].RequiredClassificationCode);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AssessDocumentCompleteness_RequirementSetNotFound_ThrowsException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();

        var command = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "NON_EXISTENT_SET",
            RequirementSetVersion: "v9.9",
            AssessedDocuments: new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) },
            ClassifiedDocuments: new List<ClassifiedDocumentDto>
            {
                new ClassifiedDocumentDto(
                    Id: Guid.NewGuid(),
                    DocumentBinding: CreateBinding(doc1Id, ver1Id),
                    OriginalClassificationCode: "IdentityProof",
                    ConfidenceScore: 0.98m,
                    Status: "Accepted",
                    ClassifiedAt: _fixedUtcTime.AddMinutes(-10))
            });

        // Act & Assert: Handler refuses to proceed without trusted requirement catalogue entry
        await Assert.ThrowsAsync<DocumentRequirementSetNotFoundException>(() => _assessHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AssessDocumentCompleteness_DifferentRequirementSetVersion_EvaluatesAccordingToThatVersion()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

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

        // v1 requires SurveyPlan and IdentityProof
        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("SurveyPlan"),
            CreateRequirement("IdentityProof")
        });

        // v2 requires ONLY IdentityProof
        _requirementProvider.RegisterSet("STANDARD_LEASE", "v2.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("IdentityProof")
        });

        // Act with v2.0
        var commandV2 = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v2.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        var resultV2 = await _assessHandler.HandleAsync(commandV2);

        // Assert: Under v2.0 requirements, it is complete!
        Assert.Equal("Complete", resultV2.Outcome);
        Assert.Empty(resultV2.MissingRequirements);
    }

    [Fact]
    public async Task ReviewDocumentClassification_ValidOfficerAction_ConfirmsClassification_UpdatesOutcome_CommitsOnce()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();
        var assessedDocs = new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) };

        var classifiedDocId = Guid.NewGuid();
        var classifiedDocs = new List<ClassifiedDocumentDto>
        {
            new ClassifiedDocumentDto(
                Id: classifiedDocId,
                DocumentBinding: CreateBinding(doc1Id, ver1Id),
                OriginalClassificationCode: "SurveyPlan",
                ConfidenceScore: 0.65m,
                Status: "Uncertain",
                ClassifiedAt: _fixedUtcTime.AddMinutes(-20))
        };

        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("SurveyPlan")
        });

        var assessCommand = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        var initialAssessment = await _assessHandler.HandleAsync(assessCommand);
        Assert.Equal("RequiresHumanReview", initialAssessment.Outcome);

        // Act: Officer confirms classification
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "ClassificationReviewer" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, doc1Id.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var reviewCommand = new ReviewDocumentClassificationCommand(
            AssessmentId: initialAssessment.Id,
            ClassifiedDocumentId: classifiedDocId,
            Decision: "Confirmed",
            CorrectedClassificationCode: null,
            Reason: null,
            ReviewingActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: initialAssessment.Revision);

        var reviewedResult = await _reviewHandler.HandleAsync(reviewCommand);

        // Assert
        Assert.NotNull(reviewedResult);
        Assert.Equal("Complete", reviewedResult.Outcome);
        Assert.Equal(2, _unitOfWork.CommitCount); // 1 assess, 1 review
    }

    [Fact]
    public async Task ReviewDocumentClassification_CorrectionAction_CorrectsClassificationCode_CommitsOnce()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();
        var assessedDocs = new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) };

        var classifiedDocId = Guid.NewGuid();
        var classifiedDocs = new List<ClassifiedDocumentDto>
        {
            new ClassifiedDocumentDto(
                Id: classifiedDocId,
                DocumentBinding: CreateBinding(doc1Id, ver1Id),
                OriginalClassificationCode: "TaxReceipt",
                ConfidenceScore: 0.90m,
                Status: "Accepted",
                ClassifiedAt: _fixedUtcTime.AddMinutes(-20))
        };

        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("SurveyPlan")
        });

        var assessCommand = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: assessedDocs,
            ClassifiedDocuments: classifiedDocs);

        var initialAssessment = await _assessHandler.HandleAsync(assessCommand);
        Assert.Equal("MissingRequiredDocuments", initialAssessment.Outcome);

        // Act: Officer corrects classification to SurveyPlan
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "ClassificationReviewer" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, doc1Id.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var reviewCommand = new ReviewDocumentClassificationCommand(
            AssessmentId: initialAssessment.Id,
            ClassifiedDocumentId: classifiedDocId,
            Decision: "Corrected",
            CorrectedClassificationCode: "SurveyPlan",
            Reason: "Document was mislabeled by OCR scanner",
            ReviewingActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: initialAssessment.Revision);

        var reviewedResult = await _reviewHandler.HandleAsync(reviewCommand);

        // Assert
        Assert.NotNull(reviewedResult);
        Assert.Equal("Complete", reviewedResult.Outcome);
        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ReviewDocumentClassification_UnauthorizedOfficer_ThrowsException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();
        var classifiedDocId = Guid.NewGuid();

        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("SurveyPlan")
        });

        var assessment = await _assessHandler.HandleAsync(new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) },
            ClassifiedDocuments: new List<ClassifiedDocumentDto>
            {
                new ClassifiedDocumentDto(
                    Id: classifiedDocId,
                    DocumentBinding: CreateBinding(doc1Id, ver1Id),
                    OriginalClassificationCode: "SurveyPlan",
                    ConfidenceScore: 0.60m,
                    Status: "Uncertain",
                    ClassifiedAt: _fixedUtcTime.AddMinutes(-20))
            }));

        var initialCommitCount = _unitOfWork.CommitCount;

        // Officer missing ClassificationReviewer capability
        var actorId = Guid.NewGuid();
        var unauthorizedAuthority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "Viewer" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, doc1Id.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var reviewCommand = new ReviewDocumentClassificationCommand(
            AssessmentId: assessment.Id,
            ClassifiedDocumentId: classifiedDocId,
            Decision: "Confirmed",
            CorrectedClassificationCode: null,
            Reason: null,
            ReviewingActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(unauthorizedAuthority),
            ExpectedRevision: assessment.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<MissingVerifiedAuthorityException>(() => _reviewHandler.HandleAsync(reviewCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ReviewDocumentClassification_StaleRevision_ThrowsConcurrencyException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();
        var classifiedDocId = Guid.NewGuid();

        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("SurveyPlan")
        });

        var assessment = await _assessHandler.HandleAsync(new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) },
            ClassifiedDocuments: new List<ClassifiedDocumentDto>
            {
                new ClassifiedDocumentDto(
                    Id: classifiedDocId,
                    DocumentBinding: CreateBinding(doc1Id, ver1Id),
                    OriginalClassificationCode: "SurveyPlan",
                    ConfidenceScore: 0.60m,
                    Status: "Uncertain",
                    ClassifiedAt: _fixedUtcTime.AddMinutes(-20))
            }));

        var initialCommitCount = _unitOfWork.CommitCount;

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "ClassificationReviewer" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, doc1Id.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var reviewCommand = new ReviewDocumentClassificationCommand(
            AssessmentId: assessment.Id,
            ClassifiedDocumentId: classifiedDocId,
            Decision: "Confirmed",
            CorrectedClassificationCode: null,
            Reason: null,
            ReviewingActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: 999); // Stale revision

        // Act & Assert
        await Assert.ThrowsAsync<DocumentCompletenessConcurrencyException>(() => _reviewHandler.HandleAsync(reviewCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetDocumentCompletenessAssessmentByIdQuery_ValidId_ReturnsAssessmentDto_WithoutCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();

        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("IdentityProof")
        });

        var assessment = await _assessHandler.HandleAsync(new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) },
            ClassifiedDocuments: new List<ClassifiedDocumentDto>
            {
                new ClassifiedDocumentDto(
                    Id: Guid.NewGuid(),
                    DocumentBinding: CreateBinding(doc1Id, ver1Id),
                    OriginalClassificationCode: "IdentityProof",
                    ConfidenceScore: 0.95m,
                    Status: "Accepted",
                    ClassifiedAt: _fixedUtcTime.AddMinutes(-10))
            }));

        var commitCount = _unitOfWork.CommitCount;

        var query = new GetDocumentCompletenessAssessmentByIdQuery(assessment.Id);

        // Act
        var result = await _getByIdHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(assessment.Id, result.Id);
        Assert.Equal("Complete", result.Outcome);
        Assert.Equal(commitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetLatestDocumentCompletenessAssessmentByCaseIdQuery_ValidCase_ReturnsLatestDto_WithoutCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        CreateCase(caseId);

        var doc1Id = Guid.NewGuid();
        var ver1Id = Guid.NewGuid();

        _requirementProvider.RegisterSet("STANDARD_LEASE", "v1.0", new List<DocumentRequirementSnapshot>
        {
            CreateRequirement("IdentityProof")
        });

        var assessment = await _assessHandler.HandleAsync(new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: "STANDARD_LEASE",
            RequirementSetVersion: "v1.0",
            AssessedDocuments: new List<AssessedDocumentBindingDto> { CreateBinding(doc1Id, ver1Id) },
            ClassifiedDocuments: new List<ClassifiedDocumentDto>
            {
                new ClassifiedDocumentDto(
                    Id: Guid.NewGuid(),
                    DocumentBinding: CreateBinding(doc1Id, ver1Id),
                    OriginalClassificationCode: "IdentityProof",
                    ConfidenceScore: 0.95m,
                    Status: "Accepted",
                    ClassifiedAt: _fixedUtcTime.AddMinutes(-10))
            }));

        var commitCount = _unitOfWork.CommitCount;

        var query = new GetLatestDocumentCompletenessAssessmentByCaseIdQuery(caseId);

        // Act
        var result = await _getLatestHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(assessment.Id, result.Id);
        Assert.Equal("Complete", result.Outcome);
        Assert.Equal(commitCount, _unitOfWork.CommitCount);
    }

    // --- Test Doubles ---

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
