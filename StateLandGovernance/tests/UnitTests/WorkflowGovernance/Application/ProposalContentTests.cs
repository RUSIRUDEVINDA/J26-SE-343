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
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public class ProposalContentTests
{
    private const string Sha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private readonly SpyLeaseCaseRepository _leaseCaseRepository;
    private readonly FakeProposalTemplateProvider _templateProvider;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;

    private readonly AssessProposalContentCommandHandler _assessHandler;
    private readonly ConfirmProposalContentAssessmentCommandHandler _confirmHandler;
    private readonly CorrectProposalContentAssessmentCommandHandler _correctHandler;
    private readonly GetProposalContentAssessmentQueryHandler _getByIdHandler;
    private readonly GetCurrentProposalContentAssessmentQueryHandler _getCurrentHandler;

    public ProposalContentTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
        _leaseCaseRepository = new SpyLeaseCaseRepository();
        _templateProvider = new FakeProposalTemplateProvider();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _assessHandler = new AssessProposalContentCommandHandler(
            _leaseCaseRepository,
            _templateProvider,
            _unitOfWork,
            new AssessProposalContentCommandValidator(),
            _timeProvider);

        _confirmHandler = new ConfirmProposalContentAssessmentCommandHandler(
            _leaseCaseRepository,
            _unitOfWork,
            new ConfirmProposalContentAssessmentCommandValidator(),
            _timeProvider);

        _correctHandler = new CorrectProposalContentAssessmentCommandHandler(
            _leaseCaseRepository,
            _templateProvider,
            _unitOfWork,
            new CorrectProposalContentAssessmentCommandValidator(),
            _timeProvider);

        _getByIdHandler = new GetProposalContentAssessmentQueryHandler(_leaseCaseRepository);
        _getCurrentHandler = new GetCurrentProposalContentAssessmentQueryHandler(_leaseCaseRepository);
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
    public async Task AssessProposalContent_AllMandatorySectionsPresent_EvaluatesToComplete_CommitsOnce()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found")
        };

        var command = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: template.Id.Value,
            TemplateVersion: template.Version,
            ExtractionReference: "ext-1",
            Observations: observations,
            ExpectedRevision: leaseCase.Revision);

        // Act
        var result = await _assessHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Complete", result.Outcome);
        Assert.Equal(3, result.SatisfiedMandatory.Count);
        Assert.Empty(result.MissingMandatory);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AssessProposalContent_MissingMandatorySection_EvaluatesToIncomplete_PreservesDeficit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Missing", null, null, null, null, "Section omitted")
        };

        var command = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: template.Id.Value,
            TemplateVersion: template.Version,
            ExtractionReference: "ext-1",
            Observations: observations,
            ExpectedRevision: leaseCase.Revision);

        // Act
        var result = await _assessHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Incomplete", result.Outcome);
        Assert.Single(result.MissingMandatory);
        Assert.Equal("PURPOSE", result.MissingMandatory[0].Id);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AssessProposalContent_TemplateNotFound_ThrowsException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);

        var command = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: "UNKNOWN_TEMPLATE",
            TemplateVersion: "v1.0",
            ExtractionReference: "ext-1",
            Observations: new List<ProposalObservationDto>(),
            ExpectedRevision: leaseCase.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<ProposalTemplateNotFoundException>(() => _assessHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AssessProposalContent_EvaluatesComplete_RemainsUnconfirmedUntilOfficerAction()
    {
        // Arrange: All mandatory sections present
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found")
        };

        var assessCommand = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: template.Id.Value,
            TemplateVersion: template.Version,
            ExtractionReference: "ext-1",
            Observations: observations,
            ExpectedRevision: leaseCase.Revision);

        // Act 1: Assess proposal content (preliminary machine evaluation)
        var preliminaryDto = await _assessHandler.HandleAsync(assessCommand);

        // Assert 1: Mechanically Complete, but NOT confirmed by officer
        Assert.Equal("Complete", preliminaryDto.Outcome);
        Assert.False(preliminaryDto.IsConfirmed);
        Assert.Null(preliminaryDto.ConfirmedByActorId);
        Assert.Null(preliminaryDto.ConfirmedAtUtc);
        Assert.Null(preliminaryDto.ConfirmationNotes);

        // Act 2: Officer confirms assessment
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { LeaseCase.ProposalReviewCapability },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var confirmCommand = new ConfirmProposalContentAssessmentCommand(
            LeaseCaseId: caseId,
            AssessmentResultId: preliminaryDto.Id,
            ActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: leaseCase.Revision,
            Notes: "Verified by review officer");

        var confirmedDto = await _confirmHandler.HandleAsync(confirmCommand);

        // Assert 2: Now authoritatively confirmed
        Assert.Equal("Complete", confirmedDto.Outcome);
        Assert.True(confirmedDto.IsConfirmed);
        Assert.Equal(actorId, confirmedDto.ConfirmedByActorId);
        Assert.NotNull(confirmedDto.ConfirmedAtUtc);
        Assert.Equal("Verified by review officer", confirmedDto.ConfirmationNotes);
    }

    [Fact]
    public async Task ConfirmProposalContentAssessment_ValidOfficerAction_ConfirmsAssessment_CommitsOnce()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found")
        };

        var assessed = await _assessHandler.HandleAsync(new AssessProposalContentCommand(
            caseId, Guid.NewGuid(), Guid.NewGuid(), "SHA-256", Sha256V1,
            template.Id.Value, template.Version, "ext-1", observations, leaseCase.Revision));

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { LeaseCase.ProposalReviewCapability },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var confirmCommand = new ConfirmProposalContentAssessmentCommand(
            LeaseCaseId: caseId,
            AssessmentResultId: assessed.Id,
            ActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: leaseCase.Revision,
            Notes: "Verified by senior land officer");

        // Act
        var result = await _confirmHandler.HandleAsync(confirmCommand);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsConfirmed);
        Assert.Equal(actorId, result.ConfirmedByActorId);
        Assert.Equal("Verified by senior land officer", result.ConfirmationNotes);
        Assert.Equal(2, _unitOfWork.CommitCount); // 1 assess, 1 confirm
    }

    [Fact]
    public async Task CorrectProposalContentAssessment_ValidOfficerAction_CreatesSupersedingAssessment_CommitsOnce()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var initialObservations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Missing", null, null, null, null, "Section omitted")
        };

        var assessed = await _assessHandler.HandleAsync(new AssessProposalContentCommand(
            caseId, Guid.NewGuid(), Guid.NewGuid(), "SHA-256", Sha256V1,
            template.Id.Value, template.Version, "ext-1", initialObservations, leaseCase.Revision));

        Assert.Equal("Incomplete", assessed.Outcome);

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { LeaseCase.ProposalReviewCapability },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var correctedObservations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found in appendix A")
        };

        var correctCommand = new CorrectProposalContentAssessmentCommand(
            LeaseCaseId: caseId,
            AssessmentResultId: assessed.Id,
            ActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: leaseCase.Revision,
            Reason: "Purpose section identified on page 14 of appendix A",
            EvidenceReference: "ev-app-a-p14",
            CorrectedObservations: correctedObservations);

        // Act
        var result = await _correctHandler.HandleAsync(correctCommand);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Complete", result.Outcome);
        Assert.Equal(assessed.Id, result.SupersedesResultId);
        Assert.Equal("Purpose section identified on page 14 of appendix A", result.CorrectionReason);
        Assert.Equal(2, _unitOfWork.CommitCount); // 1 assess, 1 correct
    }

    [Fact]
    public async Task ConfirmProposalContentAssessment_UnauthorizedOfficer_ThrowsException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found")
        };

        var assessed = await _assessHandler.HandleAsync(new AssessProposalContentCommand(
            caseId, Guid.NewGuid(), Guid.NewGuid(), "SHA-256", Sha256V1,
            template.Id.Value, template.Version, "ext-1", observations, leaseCase.Revision));

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "UnauthorizedRole" }, // Missing ProposalReviewCapability
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var confirmCommand = new ConfirmProposalContentAssessmentCommand(
            LeaseCaseId: caseId,
            AssessmentResultId: assessed.Id,
            ActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: leaseCase.Revision);

        var initialCommitCount = _unitOfWork.CommitCount;

        // Act & Assert
        await Assert.ThrowsAsync<MissingVerifiedAuthorityException>(() => _confirmHandler.HandleAsync(confirmCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CorrectProposalContentAssessment_TemplateDigestMismatch_ThrowsException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Missing", null, null, null, null, "Section omitted")
        };

        var assessed = await _assessHandler.HandleAsync(new AssessProposalContentCommand(
            caseId, Guid.NewGuid(), Guid.NewGuid(), "SHA-256", Sha256V1,
            template.Id.Value, template.Version, "ext-1", observations, leaseCase.Revision));

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { LeaseCase.ProposalReviewCapability },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        // Create a different template with different digest under the same ID/version to simulate rogue mutation in catalogue
        var alteredTemplate = new ProposalTemplate(
            template.Id,
            template.Version,
            "Altered Template Name",
            "DIFFERENT-REF",
            ProposalTemplateStatus.Active,
            template.Requirements);

        _templateProvider.RegisterTemplate(alteredTemplate);

        var correctCommand = new CorrectProposalContentAssessmentCommand(
            LeaseCaseId: caseId,
            AssessmentResultId: assessed.Id,
            ActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: leaseCase.Revision,
            Reason: "Section was present in appendix",
            EvidenceReference: "ev-appendix-p3",
            CorrectedObservations: observations);

        var initialCommitCount = _unitOfWork.CommitCount;

        // Act & Assert: Domain enforces identical definition digest on correction
        await Assert.ThrowsAsync<InvalidProposalContentReviewException>(() => _correctHandler.HandleAsync(correctCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AssessProposalContent_StaleRevision_ThrowsConcurrencyException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var command = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: Sha256V1,
            TemplateId: template.Id.Value,
            TemplateVersion: template.Version,
            ExtractionReference: "ext-1",
            Observations: new List<ProposalObservationDto>(),
            ExpectedRevision: 999); // Stale

        // Act & Assert
        await Assert.ThrowsAsync<LeaseCaseConcurrencyException>(() => _assessHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ConfirmProposalContentAssessment_StaleRevision_ThrowsConcurrencyException_NoCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found")
        };

        var assessed = await _assessHandler.HandleAsync(new AssessProposalContentCommand(
            caseId, Guid.NewGuid(), Guid.NewGuid(), "SHA-256", Sha256V1,
            template.Id.Value, template.Version, "ext-1", observations, leaseCase.Revision));

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { LeaseCase.ProposalReviewCapability },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var confirmCommand = new ConfirmProposalContentAssessmentCommand(
            LeaseCaseId: caseId,
            AssessmentResultId: assessed.Id,
            ActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedRevision: 999); // Stale

        var initialCommitCount = _unitOfWork.CommitCount;

        // Act & Assert
        await Assert.ThrowsAsync<LeaseCaseConcurrencyException>(() => _confirmHandler.HandleAsync(confirmCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetProposalContentAssessmentQuery_ValidId_ReturnsAssessmentDto_WithoutCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found")
        };

        var assessed = await _assessHandler.HandleAsync(new AssessProposalContentCommand(
            caseId, Guid.NewGuid(), Guid.NewGuid(), "SHA-256", Sha256V1,
            template.Id.Value, template.Version, "ext-1", observations, leaseCase.Revision));

        var initialCommitCount = _unitOfWork.CommitCount;

        var query = new GetProposalContentAssessmentQuery(caseId, assessed.Id);

        // Act
        var result = await _getByIdHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(assessed.Id, result.Id);
        Assert.Equal("Complete", result.Outcome);
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetCurrentProposalContentAssessmentQuery_ValidCase_ReturnsCurrentAssessmentDto_WithoutCommit()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var leaseCase = CreateCase(caseId);
        var template = CreateStandardTemplate();
        _templateProvider.RegisterTemplate(template);

        var observations = new List<ProposalObservationDto>
        {
            new ProposalObservationDto("APPLICANT_DETAILS", "Present", 1, "Applicant: Acme Corp", "ev-1", "ext-1", "Found"),
            new ProposalObservationDto("LAND_DETAILS", "Present", 2, "Plot 42, Colombo", "ev-2", "ext-2", "Found"),
            new ProposalObservationDto("PURPOSE", "Present", 3, "Eco-Tourism Resort", "ev-3", "ext-3", "Found")
        };

        var assessed = await _assessHandler.HandleAsync(new AssessProposalContentCommand(
            caseId, Guid.NewGuid(), Guid.NewGuid(), "SHA-256", Sha256V1,
            template.Id.Value, template.Version, "ext-1", observations, leaseCase.Revision));

        var initialCommitCount = _unitOfWork.CommitCount;

        var query = new GetCurrentProposalContentAssessmentQuery(caseId, assessed.TemplateSnapshot.DefinitionDigest);

        // Act
        var result = await _getCurrentHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(assessed.Id, result.Id);
        Assert.Equal("Complete", result.Outcome);
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    // --- Test Doubles ---

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
