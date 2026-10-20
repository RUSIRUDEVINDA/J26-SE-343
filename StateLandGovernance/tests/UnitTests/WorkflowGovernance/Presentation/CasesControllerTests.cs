namespace StateLandGovernance.UnitTests.WorkflowGovernance.Presentation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using StateLandGovernance.WorkflowGovernance.Presentation.DTOs;
using Xunit;

public sealed class CasesControllerTests
{
    [Fact]
    public async Task RemoteDevelopmentCaller_CannotEstablishOfficerAuthorityWithHeaders()
    {
        var controller = CreateController(Guid.NewGuid(), "Officer");
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.10");
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetExecution(Guid.NewGuid(), null!, default)).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedApplicant_CannotEscalateUsingDevelopmentOfficerHeaders()
    {
        var controller = CreateController(Guid.NewGuid(), "Officer");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            new Claim(ClaimTypes.Role, "Applicant") }, "TEST-ONLY"));
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetExecution(Guid.NewGuid(), null!, default)).StatusCode);
    }

    [Theory]
    [InlineData("WorkflowApproval:test")]
    [InlineData("WorkflowEvidence:test")]
    [InlineData("ProjectFulfillment:test")]
    public async Task ApplicantOwner_CannotListDownloadOrAnalyzeInstitutionalOutputViaGenericRoutes(string category)
    {
        var ownerId = Guid.NewGuid();
        var owner = CreateController(ownerId, "Applicant");
        var intake = Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(await owner.RegisterCaseIntake(
            CreateFormFile("proposal.pdf", "application/pdf", Encoding.UTF8.GetBytes("TEST ONLY")), "TEST-OUTPUT-ISOLATION")).Value);
        var now = DateTime.UtcNow;
        var authority = new VerifiedAuthorityContext(ownerId, ["DocumentSubmitter"], "LeaseCase", intake.LeaseCaseId.ToString("D"),
            now.AddMinutes(-1), now, now.AddHours(1));
        var output = new GovernedDocument(new(Guid.NewGuid()), new(intake.LeaseCaseId), category, new(Guid.NewGuid()),
            new("SHA-256", new string('a', 64)), new("TEST-ONLY-OUTPUT"), "internal.pdf", "application/pdf", 1,
            ownerId, now, authority.ToDomain());
        await _documentRepo.AddAsync(output);
        var detail = Assert.IsType<CaseDetailResponse>(Assert.IsType<OkObjectResult>(await owner.GetCaseDetail(intake.LeaseCaseId)).Value);
        Assert.DoesNotContain(detail.Documents, d => d.DocumentId == output.Id.Value);
        Assert.Empty(detail.FieldInventory);
        var tracking = Assert.IsType<ApplicantCaseTrackingDto>(Assert.IsType<OkObjectResult>(await owner.TrackApplication(intake.ApplicationReference)).Value);
        Assert.DoesNotContain(tracking.Documents, d => d.DocumentId == output.Id.Value);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await owner.DownloadDocumentVersion(intake.LeaseCaseId,
            output.Id.Value, output.ActiveVersionId.Value)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await owner.ReanalyzeVersion(intake.LeaseCaseId,
            output.Id.Value, output.ActiveVersionId.Value)).StatusCode);
    }

    [Fact]
    public async Task ApplicantProgress_IndependentCommissionerApproval_DoesNotMislabelRegionalRecommendationAsCurrentBlock()
    {
        var f = new StateLandGovernance.UnitTests.WorkflowGovernance.Application.ProjectWorkflowExecutionSliceTests.Fixture();
        await f.Start(); await f.Decide("CABINET", "Approved"); await f.Decide("EXTERNAL_TEST_A", "Approved");
        await f.Decide("DS", "Approved"); await f.Decide("PROVINCIAL_LC", "Rejected"); await f.Decide("COMMISSIONER", "Approved");
        await _leaseCaseRepo.AddAsync(f.Case);
        var controller = CreateController(f.Actor, "Applicant");
        var response = Assert.IsType<OkObjectResult>(await controller.ApplicantWorkflowProgress(f.Case.Id.Value, f.Service, default));
        var payload = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(0, payload.GetProperty("blockingRejections").GetArrayLength());
        Assert.Equal(1, payload.GetProperty("reviewedRegionalNonRecommendations").GetArrayLength());
        Assert.DoesNotContain("TEST_INPUT", payload.GetRawText());
    }

    [Fact]
    public async Task InstitutionalOfficer_CannotUseGeneralCaseWorkspaceToReadUnrestrictedEvidence()
    {
        var fixture = StateLandGovernance.UnitTests.WorkflowGovernance.Application.ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot();
        await _leaseCaseRepo.AddAsync(fixture.LeaseCase); await _documentRepo.AddAsync(fixture.Document); await _analysisRepo.AddAsync(fixture.Analysis);
        var controller = CreateController(Guid.NewGuid(), "Officer");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")), new Claim(ClaimTypes.Role, "Officer"),
            new Claim("institution", "TEST_EXTERNAL"), new Claim("authority_case_id", fixture.LeaseCase.Id.Value.ToString("D")) }, "TEST-ONLY"));
        controller.Request.Headers["X-Actor-Role"] = "Officer";
        var response = Assert.IsType<ObjectResult>(await controller.GetCaseDetail(fixture.LeaseCase.Id.Value));
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentOfficerWithoutCurrentAssignmentCannotListAllCases()
    {
        var controller=CreateController(Guid.NewGuid(),"Officer");
        controller.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"Officer"),
            new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim("development_account","expired-TEST-ONLY")},"TEST-ONLY"));
        Assert.Equal(403,Assert.IsType<StatusCodeResult>(await controller.ListCases()).StatusCode);
    }

    [Fact]
    public async Task DevelopmentCoordinatorIsRestrictedToAssignedCaseAndDocument()
    {
        var owner=CreateController(Guid.NewGuid(),"Applicant");
        var intake=Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(await owner.RegisterCaseIntake(
            CreateFormFile("proposal.pdf","application/pdf",Encoding.UTF8.GetBytes("TEST ONLY")),"TEST-DEV-ASSIGNMENT")).Value);
        var actor=Guid.NewGuid();var controller=CreateController(actor,"Officer");
        var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,"Officer")},"TEST-ONLY");
        controller.HttpContext.User=new ClaimsPrincipal(identity);
        identity.AddClaims(new[]{new Claim("development_account","ds-TEST-ONLY"),new Claim("institution","DS"),
            new Claim("authority_case_id",intake.LeaseCaseId.ToString("D")),new Claim("authority_document_id",intake.GovernedDocumentId.ToString("D")),
            new Claim("capability","CaseWorkspaceReviewer"),new Claim("capability","FactVerifier"),new Claim("capability","FactSnapshotPublisher"),
            new Claim("exp",DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString())});
        controller.ControllerContext.RouteData=new Microsoft.AspNetCore.Routing.RouteData();
        controller.ControllerContext.RouteData.Values["caseId"]=intake.LeaseCaseId.ToString("D");
        var detail=Assert.IsType<CaseDetailResponse>(Assert.IsType<OkObjectResult>(await controller.GetCaseDetail(intake.LeaseCaseId)).Value);
        Assert.True(detail.CanVerifyFacts);Assert.True(detail.CanPublishSnapshot);
        identity.RemoveClaim(identity.FindFirst("authority_document_id")!);
        detail=Assert.IsType<CaseDetailResponse>(Assert.IsType<OkObjectResult>(await controller.GetCaseDetail(intake.LeaseCaseId)).Value);
        Assert.False(detail.CanVerifyFacts);Assert.False(detail.CanPublishSnapshot);
        var list=Assert.IsType<PagedLeaseCasesDto>(Assert.IsType<OkObjectResult>(await controller.ListCases()).Value);
        Assert.Single(list.Items);Assert.Equal(intake.LeaseCaseId,list.Items[0].Id);
        controller.ControllerContext.RouteData.Values["caseId"]=Guid.NewGuid().ToString("D");
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.GetCaseDetail(intake.LeaseCaseId)).StatusCode);
    }

    [Fact]
    public async Task AnonymousLoopbackCallerDoesNotBecomeDefaultApplicantOwner()
    {
        var owner = CreateController(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Applicant");
        var intake = Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(await owner.RegisterCaseIntake(
            CreateFormFile("proposal.pdf", "application/pdf", Encoding.UTF8.GetBytes("TEST ONLY")), "TEST-NO-DEFAULT-IDENTITY")).Value);
        var anonymous = CreateController(Guid.NewGuid(), "Applicant");
        anonymous.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        anonymous.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        anonymous.Request.Headers.Clear();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.GetCaseDetail(intake.LeaseCaseId));
    }

    [Fact]
    public async Task OfficerRoleAlone_CannotManufactureVerifierOrPublisherAuthority()
    {
        var fixture = StateLandGovernance.UnitTests.WorkflowGovernance.Application.ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot();
        await _leaseCaseRepo.AddAsync(fixture.LeaseCase); await _documentRepo.AddAsync(fixture.Document); await _analysisRepo.AddAsync(fixture.Analysis);
        var officer = CreateController(Guid.NewGuid(), "Officer");
        var dto = StateLandGovernance.WorkflowGovernance.Application.Mappings.DocumentAnalysisMapper.ToDto(fixture.Analysis);
        var resultId = dto.ActiveRun!.Result!.Id;
        var revision = fixture.Analysis.Revision;
        Assert.Equal(403, Assert.IsType<ObjectResult>(await officer.VerifyCandidateFact(fixture.LeaseCase.Id.Value,
            new(fixture.Analysis.Id.Value, resultId, Guid.NewGuid(), revision, "Confirm", null, null, null))).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await officer.PublishSnapshot(fixture.LeaseCase.Id.Value,
            new(fixture.Analysis.Id.Value, resultId, revision, fixture.LeaseCase.Revision))).StatusCode);
        Assert.Equal(revision, fixture.Analysis.Revision);
    }

    [Fact]
    public async Task ReviewActions_RejectStaleResultInsteadOfSubstitutingTheCurrentResult()
    {
        var fixture = StateLandGovernance.UnitTests.WorkflowGovernance.Application.ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot();
        await _leaseCaseRepo.AddAsync(fixture.LeaseCase); await _documentRepo.AddAsync(fixture.Document); await _analysisRepo.AddAsync(fixture.Analysis);
        var officer = CreateController(Guid.NewGuid(), "Officer");
        var revision = fixture.Analysis.Revision;
        Assert.IsType<ConflictObjectResult>(await officer.VerifyCandidateFact(fixture.LeaseCase.Id.Value,
            new(fixture.Analysis.Id.Value, Guid.NewGuid(), Guid.NewGuid(), revision, "Confirm", null, null, null)));
        Assert.IsType<ConflictObjectResult>(await officer.PublishSnapshot(fixture.LeaseCase.Id.Value,
            new(fixture.Analysis.Id.Value, Guid.NewGuid(), revision, fixture.LeaseCase.Revision)));
        Assert.Equal(revision, fixture.Analysis.Revision);
    }
    [Fact]
    public async Task ProposalWorkspace_ExplicitSelectionNeverFallsBackToDeedOrAnotherProposal()
    {
        var officer = CreateController(Guid.NewGuid(), "Officer");
        var intake = Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(await officer.RegisterCaseIntake(
            CreateFormFile("proposal.pdf", "application/pdf", Encoding.UTF8.GetBytes("%PDF-test")), "TEST-PROPOSAL-SELECTION")).Value);
        var deed = Assert.IsType<GovernedDocumentDto>(Assert.IsType<OkObjectResult>(await officer.UploadSupportingDocument(intake.LeaseCaseId,
            CreateFormFile("deed.pdf", "application/pdf", Encoding.UTF8.GetBytes("%PDF-deed")), "Deed")).Value);
        await officer.UploadSupportingDocument(intake.LeaseCaseId,
            CreateFormFile("proposal-2.pdf", "application/pdf", Encoding.UTF8.GetBytes("%PDF-second")), "Proposal");
        var ambiguous = Assert.IsType<CaseDetailResponse>(Assert.IsType<OkObjectResult>(await officer.GetCaseDetail(intake.LeaseCaseId)).Value);
        Assert.Equal("SelectionRequired", ambiguous.ProposalSelection!.Status);
        Assert.Null(ambiguous.ProposalSelection.DocumentId);
        Assert.All(ambiguous.FieldInventory, f => Assert.Null(f.ExtractedFactId));
        var selected = Assert.IsType<CaseDetailResponse>(Assert.IsType<OkObjectResult>(await officer.GetCaseDetail(intake.LeaseCaseId,
            proposalDocumentId: intake.GovernedDocumentId)).Value);
        Assert.Equal(intake.GovernedDocumentId, selected.ProposalSelection!.DocumentId);
        Assert.Equal(intake.DocumentVersionId, selected.ProposalSelection.VersionId);
        Assert.Equal(intake.DocumentAnalysisId, selected.ProposalSelection.AnalysisId);
        Assert.False(selected.ProposalSelection.LinkedSnapshotMatchesSelection);
        Assert.IsType<NotFoundObjectResult>(await officer.GetCaseDetail(intake.LeaseCaseId, proposalDocumentId: deed.Id));
        Assert.Equal("Deed", (await _documentRepo.GetByIdAsync(new(deed.Id)))!.LogicalCategory);
    }
    [Fact]
    public async Task Reanalysis_RejectsOtherOwnerAndWrongDocumentBeforeExecution()
    {
        var owner = CreateController(Guid.NewGuid(), "Applicant");
        var intake = Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(await owner.RegisterCaseIntake(
            CreateFormFile("test.pdf", "application/pdf", Encoding.UTF8.GetBytes("%PDF-test")), "TEST-REPAIR-OWNER")).Value);
        var attacker = CreateController(Guid.NewGuid(), "Applicant");
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await attacker.ReanalyzeVersion(intake.LeaseCaseId, intake.GovernedDocumentId, intake.DocumentVersionId)).StatusCode);
        var officer = CreateController(Guid.NewGuid(), "Officer");
        Assert.IsType<NotFoundResult>(await officer.ReanalyzeVersion(intake.LeaseCaseId, Guid.NewGuid(), intake.DocumentVersionId));
    }
    [Fact]
    public async Task VerificationAndSnapshot_RejectAnalysisFromAnotherCase()
    {
        var officer = CreateController(Guid.NewGuid(), "Officer");
        async Task<RegisterCaseIntakeResponse> Intake(string reference) => Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(await officer.RegisterCaseIntake(
            CreateFormFile("test.pdf", "application/pdf", Encoding.UTF8.GetBytes("%PDF-test")), reference)).Value);
        var first = await Intake("TEST-REPAIR-CASE-A"); var second = await Intake("TEST-REPAIR-CASE-B");
        Assert.IsType<NotFoundResult>(await officer.VerifyCandidateFact(second.LeaseCaseId,
            new(first.DocumentAnalysisId!.Value, Guid.NewGuid(), Guid.NewGuid(), 1, "Confirm", null, null, null)));
        Assert.IsType<NotFoundResult>(await officer.PublishSnapshot(second.LeaseCaseId,
            new(first.DocumentAnalysisId!.Value, Guid.NewGuid(), 1, 1)));
    }
    [Theory]
    [InlineData("Applicant")]
    [InlineData("Administrator")]
    [InlineData("Officer")]
    public async Task Planning_WithoutVerifiedCaseCapabilities_GenerationAndApprovalAreForbidden(string role)
    {
        var controller = CreateController(Guid.NewGuid(), role);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GenerateWorkflowPlan(Guid.NewGuid(), new(Guid.NewGuid(), 1, null, null, null), null!, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.ApproveWorkflowPlan(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), 1, 1), null!, default)).StatusCode);
    }
    [Theory]
    [InlineData("Applicant")]
    [InlineData("Administrator")]
    public async Task Screening_WithoutOfficerRole_ReadAndRequestAreForbidden(string role)
    {
        var controller = CreateController(Guid.NewGuid(), role);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetCaseScreening(Guid.NewGuid(), null!)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.RequestCaseScreening(Guid.NewGuid(), new(Guid.NewGuid(), 1), null!)).StatusCode);
    }
    private readonly InMemoryLeaseCaseRepository _leaseCaseRepo = new();
    private readonly InMemoryGovernedDocumentRepository _documentRepo = new();
    private readonly InMemoryDocumentAnalysisRepository _analysisRepo = new();
    private readonly FakeDocumentCompletenessAssessmentRepository _completenessRepo = new();
    private readonly TestRequirementProvider _docRequirementProvider = new();
    private readonly UnconfiguredCompletenessProviders _proposalTemplateProvider = new();
    private readonly InMemoryCorrectionRepository _correctionRepo = new();
    private readonly FakeHostEnvironment _environment = new();
    private readonly InMemoryWorkflowGovernanceUnitOfWork _unitOfWork = new();
    private readonly FakeContentWriter _contentWriter = new();
    private readonly FakeContentReader _contentReader = new();
    private readonly FakeExecutionService _executionService = new();
    private readonly FakeArtifactReader _artifactReader = new();

    private CasesController CreateController(Guid? callerActorId = null, string? role = null)
    {
        var timeProvider = TimeProvider.System;
        var registerCaseHandler = new RegisterLeaseCaseCommandHandler(
            _leaseCaseRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RegisterLeaseCaseCommandValidator(), timeProvider);
        var registerDocHandler = new RegisterGovernedDocumentCommandHandler(
            _documentRepo, _leaseCaseRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RegisterGovernedDocumentCommandValidator(), timeProvider);
        var addVersionHandler = new AddDocumentVersionCommandHandler(
            _documentRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.AddDocumentVersionCommandValidator(), timeProvider);
        var requestAnalysisHandler = new RequestDocumentAnalysisCommandHandler(
            _analysisRepo, _documentRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RequestDocumentAnalysisCommandValidator(), timeProvider);
        var verifyFactHandler = new VerifyCandidateFactCommandHandler(
            _analysisRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.VerifyCandidateFactCommandValidator(), timeProvider);
        var createSnapshotHandler = new CreateVerifiedFactSnapshotCommandHandler(
            _analysisRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.CreateVerifiedFactSnapshotCommandValidator(), timeProvider);
        var linkSnapshotHandler = new LinkVerifiedFactSnapshotToCaseCommandHandler(
            _leaseCaseRepo, _analysisRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.LinkVerifiedFactSnapshotToCaseCommandValidator(), _documentRepo);
        var assessDocCompletenessHandler = new AssessDocumentCompletenessCommandHandler(
            _completenessRepo, _leaseCaseRepo, _docRequirementProvider, _unitOfWork,
            new StateLandGovernance.WorkflowGovernance.Application.Validators.AssessDocumentCompletenessCommandValidator(), timeProvider);
        var assessPropContentHandler = new AssessProposalContentCommandHandler(
            _leaseCaseRepo, _proposalTemplateProvider, _unitOfWork,
            new StateLandGovernance.WorkflowGovernance.Application.Validators.AssessProposalContentCommandValidator(), timeProvider);

        var listCasesHandler = new ListLeaseCasesQueryHandler(_leaseCaseRepo);
        var getCaseHandler = new GetLeaseCaseByIdQueryHandler(_leaseCaseRepo);
        var getDocsHandler = new GetDocumentsByLeaseCaseIdQueryHandler(_documentRepo, _leaseCaseRepo);
        var getAnalysisHandler = new GetDocumentAnalysisByVersionIdQueryHandler(_analysisRepo);
        var getAnalysisByIdHandler = new GetDocumentAnalysisByIdQueryHandler(_analysisRepo);
        var getSnapshotHandler = new GetCurrentVerifiedFactsForCaseQueryHandler(_leaseCaseRepo, _analysisRepo);
        var trackingHandler = new GetApplicantCaseTrackingQueryHandler(_leaseCaseRepo, _documentRepo, _analysisRepo, _correctionRepo);
        var getLatestDocCompletenessHandler = new GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler(_completenessRepo);
        var getCurrentPropContentHandler = new GetCurrentProposalContentAssessmentQueryHandler(_leaseCaseRepo);

        var controller = new CasesController(
            registerCaseHandler,
            registerDocHandler,
            addVersionHandler,
            requestAnalysisHandler,
            verifyFactHandler,
            createSnapshotHandler,
            linkSnapshotHandler,
            assessDocCompletenessHandler,
            assessPropContentHandler,
            listCasesHandler,
            getCaseHandler,
            getDocsHandler,
            getAnalysisHandler,
            getAnalysisByIdHandler,
            getSnapshotHandler,
            trackingHandler,
            getLatestDocCompletenessHandler,
            getCurrentPropContentHandler,
            _contentWriter,
            _contentReader,
            _documentRepo,
            _leaseCaseRepo,
            _proposalTemplateProvider,
            _executionService,
            _environment,
            NullLogger<CasesController>.Instance,
            new CorrectionWorkflowService(_correctionRepo, _leaseCaseRepo, _documentRepo, _unitOfWork, timeProvider, new InMemoryCorrectionSubmissionTransaction()),
            _correctionRepo,
            _docRequirementProvider,
            _artifactReader);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        if (callerActorId.HasValue)
        {
            httpContext.Request.Headers["X-Actor-Id"] = callerActorId.Value.ToString();
        }
        if (!string.IsNullOrEmpty(role))
        {
            httpContext.Request.Headers["X-Actor-Role"] = role;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task RegisterCaseIntake_ScopesAuthorityContextToLeaseCase_NotGlobal()
    {
        var applicantId = Guid.NewGuid();
        var controller = CreateController(callerActorId: applicantId);

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 test document content");
        var formFile = CreateFormFile("proposal.pdf", "application/pdf", pdfBytes);

        var result = await controller.RegisterCaseIntake(
            file: formFile,
            applicationReference: "APP-TEST-SCOPED-001",
            purpose: "Agricultural Lease",
            requestedExtent: 10,
            requestedExtentUnit: "Acre",
            jurisdictionCode: "Western",
            cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegisterCaseIntakeResponse>(okResult.Value);

        var persistedCase = await _leaseCaseRepo.GetByIdAsync(new LeaseCaseId(response.LeaseCaseId));
        Assert.NotNull(persistedCase);

        var persistedDoc = await _documentRepo.GetByIdAsync(new GovernedDocumentId(response.GovernedDocumentId));
        Assert.NotNull(persistedDoc);
    }

    [Fact]
    public async Task GetCaseDetail_NonOfficerAccessingAnotherApplicantsCase_Returns403Forbidden()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        // 1. Create case owned by ownerId
        var ownerController = CreateController(callerActorId: ownerId);
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 owner proposal");
        var formFile = CreateFormFile("proposal.pdf", "application/pdf", pdfBytes);

        var intakeResult = await ownerController.RegisterCaseIntake(
            file: formFile,
            applicationReference: "APP-AUTH-TEST-001",
            cancellationToken: CancellationToken.None);
        var okIntake = Assert.IsType<OkObjectResult>(intakeResult);
        var intakeRes = Assert.IsType<RegisterCaseIntakeResponse>(okIntake.Value);

        // 2. Attacker attempts to get case details without Officer role
        var attackerController = CreateController(callerActorId: attackerId, role: "Applicant");
        var detailResult = await attackerController.GetCaseDetail(intakeRes.LeaseCaseId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(detailResult);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task DownloadDocumentVersion_NonOfficerAccessingAnotherApplicantsDocument_Returns403Forbidden()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var ownerController = CreateController(callerActorId: ownerId);
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 confidential doc");
        var formFile = CreateFormFile("proposal.pdf", "application/pdf", pdfBytes);

        var intakeResult = await ownerController.RegisterCaseIntake(
            file: formFile,
            applicationReference: "APP-AUTH-TEST-002",
            cancellationToken: CancellationToken.None);
        var okIntake = Assert.IsType<OkObjectResult>(intakeResult);
        var intakeRes = Assert.IsType<RegisterCaseIntakeResponse>(okIntake.Value);

        // Attacker attempts to download without Officer role
        var attackerController = CreateController(callerActorId: attackerId, role: "Applicant");
        var downloadResult = await attackerController.DownloadDocumentVersion(
            intakeRes.LeaseCaseId,
            intakeRes.GovernedDocumentId,
            intakeRes.DocumentVersionId,
            CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(downloadResult);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task VerifyCandidateFact_NonOfficer_Returns403Forbidden()
    {
        var applicantController = CreateController(callerActorId: Guid.NewGuid(), role: "Applicant");

        var request = new VerifyFactApiRequest(
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunResultId: Guid.NewGuid(),
            ExtractedFactId: Guid.NewGuid(),
            ExpectedAnalysisRevision: 1,
            Decision: "Confirmed",
            CorrectedValue: null,
            ValueKind: null,
            Reason: null);

        var result = await applicantController.VerifyCandidateFact(Guid.NewGuid(), request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task PublishSnapshot_NonOfficer_Returns403Forbidden()
    {
        var applicantController = CreateController(callerActorId: Guid.NewGuid(), role: "Applicant");

        var request = new PublishSnapshotApiRequest(
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunResultId: Guid.NewGuid(),
            ExpectedAnalysisRevision: 1,
            ExpectedLeaseCaseRevision: 1);

        var result = await applicantController.PublishSnapshot(Guid.NewGuid(), request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetCaseDetail_Officer_CanReadInventoryWithUngroundedAndNoCandidateStatus()
    {
        var ownerId = Guid.NewGuid();
        var officerId = Guid.NewGuid();

        var ownerController = CreateController(callerActorId: ownerId);
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 proposal text");
        var formFile = CreateFormFile("proposal.pdf", "application/pdf", pdfBytes);

        var intakeResult = await ownerController.RegisterCaseIntake(
            file: formFile,
            applicationReference: "APP-INVENTORY-TEST-001",
            cancellationToken: CancellationToken.None);
        var okIntake = Assert.IsType<OkObjectResult>(intakeResult);
        var intakeRes = Assert.IsType<RegisterCaseIntakeResponse>(okIntake.Value);

        // Seed raw semantic extraction artifact with 1 ungrounded candidate and 1 no-candidate field
        var artifactJson = @"{
            ""candidate_count"": 1,
            ""all_candidates"": [
                {
                    ""field_code"": ""project_purpose"",
                    ""canonical_value"": ""Coconut Cultivation"",
                    ""confidence"": 0.95,
                    ""is_evidence_verified"": false,
                    ""evidence"": {
                        ""page_number"": 4,
                        ""quote"": ""Coconut Cultivation Purpose""
                    }
                }
            ],
            ""field_inventory"": [
                {
                    ""field_code"": ""investment_amount"",
                    ""display_name"": ""Investment Amount"",
                    ""status"": ""Missing"",
                    ""candidate_count"": 0
                }
            ]
        }";

        _artifactReader.StoredArtifacts[$"analysis-artifacts/{intakeRes.DocumentVersionId:D}/{intakeRes.AnalysisRunId:D}/semantic-extraction.json"] =
            Encoding.UTF8.GetBytes(artifactJson);

        // Officer reads case details
        var officerController = CreateController(callerActorId: officerId, role: "Officer");
        var detailResult = await officerController.GetCaseDetail(intakeRes.LeaseCaseId, CancellationToken.None);

        var okDetail = Assert.IsType<OkObjectResult>(detailResult);
        var detailRes = Assert.IsType<CaseDetailResponse>(okDetail.Value);

        Assert.Equal(17, detailRes.FieldInventory.Count);

        var ungroundedRow = detailRes.FieldInventory.First(f => f.FactCode == "project_purpose");
        Assert.Equal("Ungrounded", ungroundedRow.Status);
        Assert.Equal("Coconut Cultivation", ungroundedRow.CandidateValue);
        Assert.False(ungroundedRow.IsGrounded);
        Assert.Null(ungroundedRow.ExtractedFactId);

        var noCandidateRow = detailRes.FieldInventory.First(f => f.FactCode == "investment_amount");
        Assert.Equal("NoCandidate", noCandidateRow.Status);
        Assert.Null(noCandidateRow.CandidateValue);
    }

    private sealed class FakeContentWriter : IDocumentContentWriter
    {
        public async Task<DocumentContentReceipt> WriteAsync(
            Stream content,
            string originalFileName,
            string mediaType,
            Guid? documentId = null,
            Guid? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var docId = documentId ?? Guid.NewGuid();
            var verId = versionId ?? Guid.NewGuid();
            var hash = await System.Security.Cryptography.SHA256.HashDataAsync(content, cancellationToken);
            return DocumentContentReceipt.CreateSha256(
                contentReference: $"documents/{docId:D}/{verId:D}/source.bin",
                sha256Hex: Convert.ToHexString(hash).ToLowerInvariant(),
                originalFileName: originalFileName,
                mediaType: mediaType,
                fileSizeInBytes: content.Length);
        }
    }

    private sealed class FakeContentReader : IDocumentContentReader
    {
        public Task<Stream?> ReadContentAsync(
            GovernedDocumentId documentId,
            DocumentVersionId versionId,
            string contentReference,
            CancellationToken cancellationToken = default)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes("test content stream"));
            return Task.FromResult<Stream?>(ms);
        }
    }

    private sealed class FakeExecutionService : IDocumentAnalysisExecutionService
    {
        public Task<DocumentAnalysisExecutionResult> ExecuteAsync(
            Guid documentAnalysisId,
            Guid analysisRunId,
            int expectedAnalysisRevision,
            string? languageHint = "sin",
            string? semanticProvider = "gemini",
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentAnalysisExecutionResult(
                documentAnalysisId,
                analysisRunId,
                "Completed",
                ArtifactCount: 0));
        }
    }

    private sealed class FakeArtifactReader : IAnalysisArtifactReader
    {
        public Dictionary<string, byte[]> StoredArtifacts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            if (StoredArtifacts.TryGetValue(storageReference, out var bytes))
            {
                return Task.FromResult<ReadOnlyMemory<byte>?>(new ReadOnlyMemory<byte>(bytes));
            }
            return Task.FromResult<ReadOnlyMemory<byte>?>(null);
        }
    }

    private sealed class FakeDocumentCompletenessAssessmentRepository : IDocumentCompletenessAssessmentRepository
    {
        private readonly List<DocumentCompletenessAssessment> _assessments = new();

        public Task<DocumentCompletenessAssessment?> GetByIdAsync(DocumentCompletenessAssessmentId id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_assessments.FirstOrDefault(a => a.Id == id));
        }

        public Task<IReadOnlyList<DocumentCompletenessAssessment>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DocumentCompletenessAssessment>>(_assessments.Where(a => a.LeaseCaseId == leaseCaseId).ToList());
        }

        public Task AddAsync(DocumentCompletenessAssessment assessment, CancellationToken cancellationToken = default)
        {
            _assessments.Add(assessment);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task AssessDocumentCompleteness_ValidCase_ReturnsAssessment()
    {
        var applicantId = Guid.NewGuid();
        var officerId = Guid.NewGuid();
        var controller = CreateController(callerActorId: officerId, role: "ComplianceOfficer");

        // First register case
        var file = CreateFormFile("proposal.pdf", "application/pdf", Encoding.UTF8.GetBytes("sample proposal"));
        var registerResult = await controller.RegisterCaseIntake(
            file: file,
            applicationReference: "APP-ASSESS-DOC-001",
            purpose: "Land Lease for Agriculture",
            requestedExtent: 10m,
            requestedExtentUnit: "Acre",
            jurisdictionCode: "Western",
            cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(registerResult);
        var registerResponse = Assert.IsType<RegisterCaseIntakeResponse>(okResult.Value);

        // Run document completeness assessment
        var assessRequest = new AssessDocumentCompletenessApiRequest(
            RequirementSetIdentifier: "DOC-REQ-AGRI-V1",
            RequirementSetVersion: "1.0"
        );

        var assessResult = await controller.AssessDocumentCompleteness(registerResponse.LeaseCaseId, assessRequest, CancellationToken.None);
        var assessOk = Assert.IsType<OkObjectResult>(assessResult);
        var assessmentDto = Assert.IsType<DocumentCompletenessAssessmentDto>(assessOk.Value);

        Assert.Equal(registerResponse.LeaseCaseId, assessmentDto.LeaseCaseId);
        Assert.NotEmpty(assessmentDto.Requirements);
    }

    [Fact]
    public async Task CreateCorrectionRequest_And_Respond_UpdatesCaseState()
    {
        var applicantId = Guid.NewGuid();
        var officerId = Guid.NewGuid();
        var applicantController = CreateController(callerActorId: applicantId, role: "Applicant");
        var officerController = CreateController(callerActorId: officerId, role: "ComplianceOfficer");

        var file = CreateFormFile("proposal.pdf", "application/pdf", Encoding.UTF8.GetBytes("sample proposal"));
        var registerResult = await applicantController.RegisterCaseIntake(
            file: file,
            applicationReference: "APP-CORRECTION-001",
            purpose: "Lease Proposal",
            requestedExtent: 5m,
            requestedExtentUnit: "Acre",
            jurisdictionCode: "Western",
            cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(registerResult);
        var registerResponse = Assert.IsType<RegisterCaseIntakeResponse>(okResult.Value);

        // Officer creates correction request
        var createCorrectionReq = new CreateCorrectionApiRequest(
            RequiredAction: "UploadSupportingDocument",
            RequiredClassificationCode: "SurveyPlan",
            RequiredSectionId: null,
            OfficerInstructions: "Please provide an officially certified licensed cadastral survey plan.",
            DueDateUtc: DateTime.UtcNow.AddDays(7));

        var createResult = await officerController.CreateCorrectionRequest(registerResponse.LeaseCaseId, createCorrectionReq, CancellationToken.None);
        var createOk = Assert.IsType<OkObjectResult>(createResult);
        var correctionDto = Assert.IsType<CaseCorrectionRequestDto>(createOk.Value);

        Assert.Equal("SurveyPlan", correctionDto.RequiredClassificationCode);
        Assert.Equal("Pending", correctionDto.Status);

        // Applicant tracking verifies correction is visible without internal notes
        var trackingResult = await applicantController.TrackApplication(registerResponse.ApplicationReference, CancellationToken.None);
        var trackingOk = Assert.IsType<OkObjectResult>(trackingResult);
        var trackingDto = Assert.IsType<ApplicantCaseTrackingDto>(trackingOk.Value);

        Assert.NotEmpty(trackingDto.CorrectionNotices);
        var notice = trackingDto.CorrectionNotices.First();
        Assert.Equal("SurveyPlan", notice.RequiredClassificationCode);
        Assert.Equal(createCorrectionReq.OfficerInstructions, notice.Instructions);

        // Applicant responds with file
        var surveyFile = CreateFormFile("survey_plan.pdf", "application/pdf", Encoding.UTF8.GetBytes("survey content"));
        var respondResult = await applicantController.RespondToCorrection(
            registerResponse.LeaseCaseId,
            correctionDto.Id,
            surveyFile,
            "Attached certified cadastral survey plan from District Land Office.",
            CancellationToken.None);

        var respondOk = Assert.IsType<OkObjectResult>(respondResult);
        var updatedCorrection = Assert.IsType<CaseCorrectionRequestDto>(respondOk.Value);
        Assert.NotNull(updatedCorrection.RespondedDocumentId);
        Assert.Equal("SubmittedAwaitingReview", updatedCorrection.Status);
        var domainCase = await _leaseCaseRepo.GetByIdAsync(new LeaseCaseId(registerResponse.LeaseCaseId));
        Assert.Equal("Pending", domainCase!.DocumentSubmissionRequirements.Single().Status.ToString());

        var unauthorized = await applicantController.ReviewCorrection(registerResponse.LeaseCaseId,
            correctionDto.Id, new ReviewCorrectionApiRequest(true, "Checked submission", 1));
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(unauthorized).StatusCode);
        var stale = await officerController.ReviewCorrection(registerResponse.LeaseCaseId,
            correctionDto.Id, new ReviewCorrectionApiRequest(true, "Checked submission", 0));
        Assert.IsType<ConflictObjectResult>(stale);
        var reviewed = await officerController.ReviewCorrection(registerResponse.LeaseCaseId,
            correctionDto.Id, new ReviewCorrectionApiRequest(true, "Checked submission", 1));
        Assert.Equal("AcceptedAwaitingReassessment", Assert.IsType<CaseCorrectionRequestDto>(Assert.IsType<OkObjectResult>(reviewed).Value).Status);
        Assert.Equal("Fulfilled", domainCase.DocumentSubmissionRequirements.Single().Status.ToString());
        var reassessed = await officerController.AssessDocumentCompleteness(registerResponse.LeaseCaseId,
            new AssessDocumentCompletenessApiRequest(), CancellationToken.None);
        Assert.Equal("Complete", Assert.IsType<DocumentCompletenessAssessmentDto>(Assert.IsType<OkObjectResult>(reassessed).Value).Outcome);
    }

    // Explicit unit-test fixture. This is not approved runtime configuration or statutory policy.
    private sealed class TestRequirementProvider : IDocumentRequirementProvider
    {
        public bool Configured { get; set; } = true;
        public Task<IReadOnlyList<DocumentRequirementSnapshot>?> GetRequirementSetAsync(string identifier, string version,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DocumentRequirementSnapshot>?>(Configured ? new[] {
                new DocumentRequirementSnapshot(new DocumentRequirementId(Guid.Parse("00000001-0000-0000-0000-000000000001")),
                    new DocumentClassificationCode("SurveyPlan"), RequirementCriticality.Mandatory,
                    RequirementApplicability.Required, 1, "TEST_FIXTURE", "Test-only survey requirement") } : null);
    }

    [Fact]
    public async Task Completeness_WithoutConfiguration_IsUnavailableAndNeverPassed()
    {
        _docRequirementProvider.Configured = false;
        var controller = CreateController(Guid.NewGuid(), "Officer");
        var intake = Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(
            await controller.RegisterCaseIntake(CreateFormFile("proposal.pdf", "application/pdf", Encoding.UTF8.GetBytes("fixture")),
                "TEST-UNCONFIGURED")).Value);
        Assert.IsType<ConflictObjectResult>(await controller.AssessDocumentCompleteness(intake.LeaseCaseId));
        Assert.IsType<ConflictObjectResult>(await controller.AssessProposalContent(intake.LeaseCaseId));
        var summary = Assert.IsType<CaseCompletenessSummaryResponse>(Assert.IsType<OkObjectResult>(
            await controller.GetCompletenessSummary(intake.LeaseCaseId)).Value);
        Assert.Null(summary.DocumentCompleteness);
        Assert.Null(summary.ProposalContent);
        Assert.False(summary.DocumentRequirementsConfigured);
        Assert.False(summary.ProposalTemplateConfigured);
        Assert.False(summary.IsDocumentCompletenessCurrent);
        Assert.False(summary.IsProposalContentCurrent);
    }

    [Fact]
    public async Task ProposalCorrection_NewVersion_PreservesPredecessorAndRejectsStaleReview()
    {
        var applicant = CreateController(Guid.NewGuid(), "Applicant");
        var officer = CreateController(Guid.NewGuid(), "Officer");
        var intake = Assert.IsType<RegisterCaseIntakeResponse>(Assert.IsType<OkObjectResult>(
            await applicant.RegisterCaseIntake(CreateFormFile("proposal.pdf", "application/pdf", Encoding.UTF8.GetBytes("fixture")),
                "TEST-VERSION-HISTORY")).Value);
        async Task<CaseCorrectionRequestDto> RequestAndUpload()
        {
            var request = Assert.IsType<CaseCorrectionRequestDto>(Assert.IsType<OkObjectResult>(await officer.CreateCorrectionRequest(
                intake.LeaseCaseId, new CreateCorrectionApiRequest("ReviseProposal", "Proposal", "project_title", "Revise fixture", DateTime.UtcNow.AddDays(1)))).Value);
            return Assert.IsType<CaseCorrectionRequestDto>(Assert.IsType<OkObjectResult>(await applicant.RespondToCorrection(intake.LeaseCaseId,
                request.Id, CreateFormFile("revised.pdf", "application/pdf", Encoding.UTF8.GetBytes("revised fixture " + request.Id)))).Value);
        }
        var first = await RequestAndUpload();
        await RequestAndUpload();
        var document = await _documentRepo.GetByIdAsync(new GovernedDocumentId(intake.GovernedDocumentId));
        Assert.Equal(3, document!.Versions.Count);
        Assert.Contains(document.Versions, v => v.Id.Value == intake.DocumentVersionId);
        Assert.Equal(intake.DocumentVersionId, document.Versions.Single(v => v.VersionNumber == 2).PredecessorVersionId!.Value.Value);
        var stale = await officer.ReviewCorrection(intake.LeaseCaseId, first.Id,
            new ReviewCorrectionApiRequest(true, "Must reject old version", first.Revision));
        Assert.IsType<ConflictObjectResult>(stale);
    }
}
