namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Presentation.DTOs;

[ApiController]
[CaseManagementExceptionFilter]
[Route("api/workflow-governance/cases")]
public sealed partial class CasesController : ControllerBase
{
    private static readonly IReadOnlyList<(string Code, string Name)> Canonical17Fields = new (string, string)[]
    {
        ("applicant_name", "Applicant Name"),
        ("applicant_type", "Applicant Type"),
        ("project_title", "Project Title"),
        ("project_purpose", "Project Purpose"),
        ("land_location", "Land Location"),
        ("district", "District"),
        ("divisional_secretariat_division", "Divisional Secretariat Division"),
        ("land_extent", "Land Extent"),
        ("land_extent_unit", "Land Extent Unit"),
        ("requested_lease_period", "Requested Lease Period"),
        ("proposed_land_use", "Proposed Land Use"),
        ("investment_amount", "Investment Amount"),
        ("investment_currency", "Investment Currency"),
        ("expected_employment", "Expected Employment"),
        ("project_description", "Project Description"),
        ("supporting_document_references", "Supporting Document References"),
        ("legal_statutory_basis", "Legal / Statutory Basis")
    };

    private readonly ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto> _registerCaseHandler;
    private readonly ICommandHandler<RegisterGovernedDocumentCommand, GovernedDocumentDto> _registerDocHandler;
    private readonly ICommandHandler<AddDocumentVersionCommand, GovernedDocumentDto> _addVersionHandler;
    private readonly ICommandHandler<RequestDocumentAnalysisCommand, DocumentAnalysisDto> _requestAnalysisHandler;
    private readonly ICommandHandler<VerifyCandidateFactCommand, HumanFactVerificationDto> _verifyFactHandler;
    private readonly ICommandHandler<CreateVerifiedFactSnapshotCommand, VerifiedFactSnapshotDto> _createSnapshotHandler;
    private readonly ICommandHandler<LinkVerifiedFactSnapshotToCaseCommand, LeaseCaseDto> _linkSnapshotHandler;
    private readonly ICommandHandler<AssessDocumentCompletenessCommand, DocumentCompletenessAssessmentDto> _assessDocCompletenessHandler;
    private readonly ICommandHandler<AssessProposalContentCommand, ProposalContentAssessmentDto> _assessPropContentHandler;
    private readonly IQueryHandler<ListLeaseCasesQuery, PagedLeaseCasesDto> _listCasesHandler;
    private readonly IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto> _getCaseHandler;
    private readonly IQueryHandler<GetDocumentsByLeaseCaseIdQuery, IReadOnlyList<GovernedDocumentDto>> _getDocsHandler;
    private readonly IQueryHandler<GetDocumentAnalysisByVersionIdQuery, DocumentAnalysisDto> _getAnalysisHandler;
    private readonly IQueryHandler<GetDocumentAnalysisByIdQuery, DocumentAnalysisDto> _getAnalysisByIdHandler;
    private readonly IQueryHandler<GetCurrentVerifiedFactsForCaseQuery, VerifiedFactSnapshotDto?> _getSnapshotHandler;
    private readonly IQueryHandler<GetApplicantCaseTrackingQuery, ApplicantCaseTrackingDto?> _trackingHandler;
    private readonly IQueryHandler<GetLatestDocumentCompletenessAssessmentByCaseIdQuery, DocumentCompletenessAssessmentDto?> _getLatestDocCompletenessHandler;
    private readonly IQueryHandler<GetCurrentProposalContentAssessmentQuery, ProposalContentAssessmentDto?> _getCurrentPropContentHandler;
    private readonly IDocumentContentWriter _contentWriter;
    private readonly IDocumentContentReader _contentReader;
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IProposalTemplateProvider _proposalTemplateProvider;
    private readonly IDocumentAnalysisExecutionService _executionService;
    private readonly IAnalysisArtifactReader? _artifactReader;
    private readonly IProposalTemplateMappingService? _templateMapping;
    private readonly Microsoft.Extensions.Hosting.IHostEnvironment _environment;
    private readonly ILogger<CasesController> _logger;
    private readonly CorrectionWorkflowService _corrections;
    private readonly ICorrectionRepository _correctionRepository;
    private readonly IDocumentRequirementProvider _documentRequirementProvider;

    public CasesController(
        ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto> registerCaseHandler,
        ICommandHandler<RegisterGovernedDocumentCommand, GovernedDocumentDto> registerDocHandler,
        ICommandHandler<AddDocumentVersionCommand, GovernedDocumentDto> addVersionHandler,
        ICommandHandler<RequestDocumentAnalysisCommand, DocumentAnalysisDto> requestAnalysisHandler,
        ICommandHandler<VerifyCandidateFactCommand, HumanFactVerificationDto> verifyFactHandler,
        ICommandHandler<CreateVerifiedFactSnapshotCommand, VerifiedFactSnapshotDto> createSnapshotHandler,
        ICommandHandler<LinkVerifiedFactSnapshotToCaseCommand, LeaseCaseDto> linkSnapshotHandler,
        ICommandHandler<AssessDocumentCompletenessCommand, DocumentCompletenessAssessmentDto> assessDocCompletenessHandler,
        ICommandHandler<AssessProposalContentCommand, ProposalContentAssessmentDto> assessPropContentHandler,
        IQueryHandler<ListLeaseCasesQuery, PagedLeaseCasesDto> listCasesHandler,
        IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto> getCaseHandler,
        IQueryHandler<GetDocumentsByLeaseCaseIdQuery, IReadOnlyList<GovernedDocumentDto>> getDocsHandler,
        IQueryHandler<GetDocumentAnalysisByVersionIdQuery, DocumentAnalysisDto> getAnalysisHandler,
        IQueryHandler<GetDocumentAnalysisByIdQuery, DocumentAnalysisDto> getAnalysisByIdHandler,
        IQueryHandler<GetCurrentVerifiedFactsForCaseQuery, VerifiedFactSnapshotDto?> getSnapshotHandler,
        IQueryHandler<GetApplicantCaseTrackingQuery, ApplicantCaseTrackingDto?> trackingHandler,
        IQueryHandler<GetLatestDocumentCompletenessAssessmentByCaseIdQuery, DocumentCompletenessAssessmentDto?> getLatestDocCompletenessHandler,
        IQueryHandler<GetCurrentProposalContentAssessmentQuery, ProposalContentAssessmentDto?> getCurrentPropContentHandler,
        IDocumentContentWriter contentWriter,
        IDocumentContentReader contentReader,
        IGovernedDocumentRepository documentRepository,
        ILeaseCaseRepository leaseCaseRepository,
        IProposalTemplateProvider proposalTemplateProvider,
        IDocumentAnalysisExecutionService executionService,
        Microsoft.Extensions.Hosting.IHostEnvironment environment,
        ILogger<CasesController> logger,
        CorrectionWorkflowService corrections,
        ICorrectionRepository correctionRepository,
        IDocumentRequirementProvider documentRequirementProvider,
        IAnalysisArtifactReader? artifactReader = null,
        IProposalTemplateMappingService? templateMapping = null)
    {
        _registerCaseHandler = registerCaseHandler;
        _registerDocHandler = registerDocHandler;
        _addVersionHandler = addVersionHandler;
        _requestAnalysisHandler = requestAnalysisHandler;
        _verifyFactHandler = verifyFactHandler;
        _createSnapshotHandler = createSnapshotHandler;
        _linkSnapshotHandler = linkSnapshotHandler;
        _assessDocCompletenessHandler = assessDocCompletenessHandler;
        _assessPropContentHandler = assessPropContentHandler;
        _listCasesHandler = listCasesHandler;
        _getCaseHandler = getCaseHandler;
        _getDocsHandler = getDocsHandler;
        _getAnalysisHandler = getAnalysisHandler;
        _getAnalysisByIdHandler = getAnalysisByIdHandler;
        _getSnapshotHandler = getSnapshotHandler;
        _trackingHandler = trackingHandler;
        _getLatestDocCompletenessHandler = getLatestDocCompletenessHandler;
        _getCurrentPropContentHandler = getCurrentPropContentHandler;
        _contentWriter = contentWriter;
        _contentReader = contentReader;
        _documentRepository = documentRepository;
        _leaseCaseRepository = leaseCaseRepository;
        _proposalTemplateProvider = proposalTemplateProvider;
        _executionService = executionService;
        _environment = environment;
        _logger = logger;
        _artifactReader = artifactReader;
        _templateMapping = templateMapping;
        _corrections = corrections;
        _correctionRepository = correctionRepository;
        _documentRequirementProvider = documentRequirementProvider;
    }

    /// <summary>
    /// Applicant Case Intake with immediate Proposal document registration and intelligence execution.
    /// </summary>
    [HttpPost("intake")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RegisterCaseIntakeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCaseIntake(
        [FromForm] IFormFile? file,
        [FromForm] string applicationReference,
        [FromForm] string? purpose = null,
        [FromForm] decimal? requestedExtent = null,
        [FromForm] string? requestedExtentUnit = null,
        [FromForm] string? jurisdictionCode = null,
        [FromForm] string? languageMode = "sin",
        [FromForm] string? semanticProvider = "gemini",
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "Proposal PDF document file is required for intake." });
        }

        if (string.IsNullOrWhiteSpace(applicationReference))
        {
            return BadRequest(new { error = "ApplicationReference is required." });
        }

        var actorId = ResolveActorId();
        var now = DateTime.UtcNow;
        var caseId = Guid.NewGuid();

        // 1. Build trusted server-side authority context scoped to LeaseCase
        var caseAuthority = new VerifiedAuthorityContext(
            ActorId: actorId,
            Capabilities: new[] { "LeaseInitiator", "DocumentSubmitter" },
            ScopeKind: "LeaseCase",
            ScopeTargetIdentifier: caseId.ToString("D"),
            ValidFrom: now.AddMinutes(-5),
            VerificationTime: now,
            ValidUntil: now.AddHours(2)
        );

        var intakeInput = new ProposalIntakeInput(
            Purpose: purpose,
            RequestedExtentValue: requestedExtent,
            RequestedExtentUnit: requestedExtentUnit,
            JurisdictionCode: jurisdictionCode,
            Region: null,
            District: null,
            SourceReference: applicationReference.Trim(),
            SourceVersion: "v1"
        );

        // Step 1: Register LeaseCase aggregate
        LeaseCaseDto leaseCase;
        try
        {
            var registerCaseCmd = new RegisterLeaseCaseCommand(
                ApplicationReference: applicationReference.Trim(),
                ActorId: actorId,
                AuthorityContext: caseAuthority,
                ProposalIntake: intakeInput,
                LeaseCaseId: caseId
            );

            leaseCase = await _registerCaseHandler.HandleAsync(registerCaseCmd, cancellationToken);
        }
        catch (DuplicateApplicationReferenceException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        // Persist intake responsibility before storage/OCR. No network call holds a database transaction.
        var assignmentStore = HttpContext.RequestServices?.GetService(typeof(ICaseAssignmentStore)) as ICaseAssignmentStore;
        if (assignmentStore != null) await assignmentStore.EnsureAsync(caseId, cancellationToken);

        // Step 2: Write raw uploaded bytes to storage & obtain authoritative SHA-256 receipt
        var docId = Guid.NewGuid();
        var verId = Guid.NewGuid();
        DocumentContentReceipt receipt;
        using (var stream = file.OpenReadStream())
        {
            receipt = await _contentWriter.WriteAsync(
                content: stream,
                originalFileName: file.FileName,
                mediaType: file.ContentType ?? "application/pdf",
                documentId: docId,
                versionId: verId,
                cancellationToken: cancellationToken
            );
        }

        // Step 3: Register GovernedDocument for the case (scoped to the LeaseCase)
        var docAuthority = new VerifiedAuthorityContext(
            ActorId: actorId,
            Capabilities: new[] { "DocumentSubmitter" },
            ScopeKind: "LeaseCase",
            ScopeTargetIdentifier: leaseCase.Id.ToString("D"),
            ValidFrom: now.AddMinutes(-5),
            VerificationTime: now,
            ValidUntil: now.AddHours(2)
        );

        var registerDocCmd = new RegisterGovernedDocumentCommand(
            LeaseCaseId: leaseCase.Id,
            LogicalCategory: "Proposal",
            ActorId: actorId,
            AuthorityContext: docAuthority,
            ContentReceipt: receipt,
            GovernedDocumentId: docId,
            InitialVersionId: verId
        );

        var governedDoc = await _registerDocHandler.HandleAsync(registerDocCmd, cancellationToken);

        // Step 4: Request DocumentAnalysis run
        var requestAnalysisCmd = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: governedDoc.Id,
            DocumentVersionId: verId,
            ModelProvider: "PythonFastApi",
            ModelName: semanticProvider ?? "gemini",
            ModelVersion: "v1",
            RequestedCapabilities: new[] { "Ocr", "SemanticExtraction" },
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunId: Guid.NewGuid()
        );

        var analysisDto = await _requestAnalysisHandler.HandleAsync(requestAnalysisCmd, cancellationToken);
        var runId = analysisDto.ActiveRun!.Id;

        // Step 5: Execute analysis pipeline safely
        string analysisExecutionStatus = "Requested";
        int candidateCount = 0;
        string? failureReason = null;

        try
        {
            var execResult = await _executionService.ExecuteAsync(
                documentAnalysisId: analysisDto.Id,
                analysisRunId: runId,
                expectedAnalysisRevision: analysisDto.Revision,
                languageHint: languageMode,
                semanticProvider: semanticProvider,
                cancellationToken: cancellationToken
            );

            analysisExecutionStatus = execResult.Status;

            var updatedAnalysis = await _getAnalysisHandler.HandleAsync(
                new GetDocumentAnalysisByVersionIdQuery(verId),
                cancellationToken
            );

            candidateCount = updatedAnalysis.ActiveRun?.Result?.ExtractedCandidateFacts.Count ?? 0;

            if (execResult.Status == "Failed")
            {
                failureReason = execResult.FailureDescription ?? execResult.FailureCode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Document intelligence pipeline execution failed for version {VersionId}. Preserving case {CaseId} and document {DocId}.", verId, leaseCase.Id, governedDoc.Id);
            analysisExecutionStatus = "Failed";
            failureReason = ex.Message;
        }

        return Ok(new RegisterCaseIntakeResponse(
            LeaseCaseId: leaseCase.Id,
            ApplicationReference: leaseCase.ApplicationReference,
            Status: leaseCase.Status,
            GovernedDocumentId: governedDoc.Id,
            DocumentVersionId: verId,
            DocumentAnalysisId: analysisDto.Id,
            AnalysisRunId: runId,
            AnalysisExecutionStatus: analysisExecutionStatus,
            ExtractedCandidatesCount: candidateCount,
            AnalysisFailureReason: failureReason
        ));
    }

    /// <summary>
    /// Officer or Applicant list of cases with bounded pagination and owner filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedLeaseCasesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCases(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] bool myCasesOnly = false,
        CancellationToken cancellationToken = default)
    {
        var assignmentStore = HttpContext.RequestServices?.GetService(typeof(ICaseAssignmentStore)) as ICaseAssignmentStore;
        if (assignmentStore != null && User.Identity?.IsAuthenticated == true && !User.IsInRole("Applicant"))
        {
            var managed = await assignmentStore.ListAsync(null, ResolveActorId(), null, false, cancellationToken);
            var ids = managed.Select(x => x.CaseId).ToList();
            if (User.HasClaim("capability", "CaseWorkspaceReviewer") && Guid.TryParse(User.FindFirstValue("authority_case_id"), out var legacy) &&
                await assignmentStore.ReadAsync(legacy, cancellationToken) == null) ids.Add(legacy);
            var items = new List<LeaseCaseDto>();
            foreach (var id in ids.Distinct().Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100)))
                items.Add(await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(id), cancellationToken));
            return Ok(new PagedLeaseCasesDto(items, ids.Distinct().Count(), skip, take));
        }
        if (User.HasClaim(c => c.Type == "development_account") && User.HasClaim("capability", "CaseWorkspaceReviewer") &&
            Guid.TryParse(User.FindFirstValue("authority_case_id"), out var assignedCase))
        {
            var assigned = await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(assignedCase), cancellationToken);
            var items = skip == 0 && take > 0 ? new[] { assigned } : Array.Empty<LeaseCaseDto>();
            return Ok(new PagedLeaseCasesDto(items, 1, skip, take));
        }
        if (User.HasClaim(c => c.Type == "development_account") && !User.IsInRole("Applicant"))
            return StatusCode(403);
        var isOfficer = IsOfficer();
        Guid? filterActorId = null;

        if (!isOfficer)
        {
            // Non-officers (applicants) are strictly restricted to their own submitted cases
            filterActorId = ResolveActorId();
        }
        else if (myCasesOnly)
        {
            filterActorId = ResolveActorId();
        }

        var result = await _listCasesHandler.HandleAsync(
            new ListLeaseCasesQuery(skip, take, filterActorId),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Case Workspace Detail view with 17-field canonical inventory and document analysis states.
    /// </summary>
    [HttpGet("{caseId:guid}")]
    [ProducesResponseType(typeof(CaseDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCaseDetail(
        Guid caseId,
        CancellationToken cancellationToken = default,
        [FromQuery] Guid? proposalDocumentId = null)
    {
        LeaseCaseDto leaseCase;
        try
        {
            leaseCase = await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(caseId), cancellationToken);
        }
        catch (LeaseCaseNotFoundException)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }

        var isOfficer = IsOfficer();
        var callerActorId = ResolveActorId();
        if (!isOfficer && leaseCase.CreatedByActorId != callerActorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Applicants may only access their own cases." });
        }

        var docs = await _getDocsHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(caseId), cancellationToken);

        // Institutional outputs are available through their explicitly scoped evidence APIs.
        // Case ownership alone does not release another institution's internal documents.
        if (!isOfficer)
            docs = docs.Where(d => Application.Mappings.CaseInputDocuments.IsIntakeInput(d.LogicalCategory)).ToArray();

        var docSummaries = new List<CaseDocumentSummaryDto>();
        DocumentAnalysisDto? proposalAnalysis = null;
        var proposals = docs.Where(d => d.LogicalCategory.Equals("Proposal", StringComparison.OrdinalIgnoreCase)).ToList();
        var selectedProposal = proposalDocumentId.HasValue
            ? proposals.SingleOrDefault(d => d.Id == proposalDocumentId.Value)
            : proposals.Count == 1 ? proposals[0] : null;
        if (proposalDocumentId.HasValue && selectedProposal is null) return NotFound(new { error = "The selected Proposal is not on this case." });

        foreach (var doc in docs)
        {
            var activeVer = doc.Versions.SingleOrDefault(v => v.Id == doc.ActiveVersionId);
            DocumentAnalysisDto? analysis = null;
            if (activeVer != null)
            {
                try
                {
                    analysis = await _getAnalysisHandler.HandleAsync(
                        new GetDocumentAnalysisByVersionIdQuery(activeVer.Id),
                        cancellationToken);
                }
                catch (DocumentAnalysisNotFoundException)
                {
                    // No analysis yet
                }
            }

            if (doc.Id == selectedProposal?.Id && analysis != null)
            {
                proposalAnalysis = analysis;
            }

            docSummaries.Add(new CaseDocumentSummaryDto(
                DocumentId: doc.Id,
                LogicalCategory: doc.LogicalCategory,
                ActiveVersionId: activeVer?.Id ?? Guid.Empty,
                VersionNumber: activeVer?.VersionNumber ?? 0,
                OriginalFileName: activeVer?.OriginalFileName ?? doc.ActiveVersion.OriginalFileName,
                MediaType: activeVer?.MediaType ?? doc.ActiveVersion.MediaType,
                FileSizeBytes: activeVer?.FileSizeInBytes ?? 0,
                ChecksumValue: activeVer?.ChecksumValue ?? "",
                CreatedAt: activeVer?.SubmittedAt ?? DateTime.UtcNow,
                DocumentAnalysisId: analysis?.Id,
                DocumentAnalysisRevision: analysis?.Revision ?? 0,
                LatestRunResultId: analysis?.ActiveRun?.Result?.Id,
                AnalysisRunCount: analysis?.RunCount ?? 0,
                LatestRunState: analysis?.ActiveRun?.State,
                LatestRunCandidatesCount: analysis?.ActiveRun?.Result?.ExtractedCandidateFacts.Count ?? 0,
                LatestRunFailureCode: analysis?.ActiveRun?.FailureCode,
                LatestRunFailureDescription: analysis?.ActiveRun?.FailureDescription
            ));
        }

        // Build the 17-field inventory
        var fieldRows = isOfficer ? await Build17FieldInventoryAsync(proposalAnalysis, cancellationToken)
            : Array.Empty<CaseGovernedFieldRowDto>();

        // Fetch published snapshot if present
        CaseVerifiedSnapshotSummaryDto? snapshotSummary = null;
        var linkedSnapshotMatchesProposal = false;
        if (leaseCase.CurrentVerifiedFactSnapshotId.HasValue && leaseCase.CurrentVerifiedFactSnapshotId.Value != Guid.Empty)
        {
            var snap = await _getSnapshotHandler.HandleAsync(
                new GetCurrentVerifiedFactsForCaseQuery(leaseCase.Id),
                cancellationToken);

            if (snap != null)
            {
                linkedSnapshotMatchesProposal = snap.GovernedDocumentId == selectedProposal?.Id
                    && snap.DocumentVersionId == selectedProposal?.ActiveVersionId
                    && snap.DocumentAnalysisId == proposalAnalysis?.Id && snap.AnalysisRunId == proposalAnalysis?.ActiveRun?.Id;
                snapshotSummary = new CaseVerifiedSnapshotSummaryDto(
                    SnapshotId: snap.Id,
                    DocumentAnalysisId: snap.DocumentAnalysisId,
                    SnapshotOutcome: snap.SnapshotOutcome,
                    PublishedAt: snap.PublishedAt,
                    PublishingActorId: snap.PublishingActorId,
                    PublishedFactCount: snap.PublishedFactCount,
                    ConfirmedFactCount: snap.ConfirmedFactCount,
                    CorrectedFactCount: snap.CorrectedFactCount
                );
            }
        }

        return Ok(new CaseDetailResponse(
            LeaseCaseId: leaseCase.Id,
            ApplicationReference: leaseCase.ApplicationReference,
            CreatedByActorId: leaseCase.CreatedByActorId,
            CreatedAt: leaseCase.CreatedAt,
            Status: leaseCase.Status,
            Revision: leaseCase.Revision,
            CurrentVerifiedFactSnapshotId: leaseCase.CurrentVerifiedFactSnapshotId,
            Purpose: leaseCase.ProposalIntake?.Purpose,
            RequestedExtent: leaseCase.ProposalIntake?.RequestedExtent,
            RequestedExtentUnit: leaseCase.ProposalIntake?.RequestedExtentUnit,
            JurisdictionCode: leaseCase.ProposalIntake?.JurisdictionCode,
            Documents: docSummaries,
            Snapshot: snapshotSummary,
            FieldInventory: fieldRows,
            ProposalSelection: new(selectedProposal?.Id, selectedProposal?.ActiveVersionId, proposalAnalysis?.Id,
                proposalAnalysis?.ActiveRun?.Id, proposalAnalysis?.ActiveRun?.Result?.Id,
                selectedProposal is null ? proposals.Count > 1 ? "SelectionRequired" : "NoProposal" : proposalAnalysis?.ActiveRun?.State ?? "NotAnalyzed",
                selectedProposal is null ? proposals.Count > 1 ? "Select the Proposal to review; no document is chosen automatically." : "Attach a new document with Proposal intake category. Existing documents and verification history remain unchanged."
                    : snapshotSummary is not null && !linkedSnapshotMatchesProposal ? "The linked case snapshot belongs to another document/version/run; its verification is not transferred to this Proposal." : null,
                linkedSnapshotMatchesProposal),
            CanVerifyFacts: selectedProposal != null && ExecutionAuthority(caseId, "FactVerifier") != null && User.HasClaim("authority_document_id", selectedProposal.Id.ToString("D")),
            CanPublishSnapshot: selectedProposal != null && ExecutionAuthority(caseId, "FactSnapshotPublisher") != null && User.HasClaim("authority_document_id", selectedProposal.Id.ToString("D"))
        ));
    }

    /// <summary>
    /// Attach an additional supporting document to an existing case.
    /// </summary>
    [HttpPost("{caseId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(GovernedDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadSupportingDocument(
        Guid caseId,
        [FromForm] IFormFile? file,
        [FromForm] string logicalCategory = "SupportingDocument",
        CancellationToken cancellationToken = default)
    {
        if (logicalCategory.StartsWith("WorkflowApproval:", StringComparison.OrdinalIgnoreCase) ||
            logicalCategory.StartsWith("WorkflowEvidence:", StringComparison.OrdinalIgnoreCase) ||
            logicalCategory.StartsWith("ProjectFulfillment:", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Institutional document namespaces require the scoped institutional/evidence API." });
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        LeaseCaseDto leaseCase;
        try
        {
            leaseCase = await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(caseId), cancellationToken);
        }
        catch (LeaseCaseNotFoundException)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }

        var isOfficer = IsOfficer();
        var actorId = ResolveActorId();
        if (!isOfficer && leaseCase.CreatedByActorId != actorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Applicants may only attach documents to their own cases." });
        }

        var now = DateTime.UtcNow;
        var docId = Guid.NewGuid();
        var verId = Guid.NewGuid();

        DocumentContentReceipt receipt;
        using (var stream = file.OpenReadStream())
        {
            receipt = await _contentWriter.WriteAsync(
                content: stream,
                originalFileName: file.FileName,
                mediaType: file.ContentType ?? "application/octet-stream",
                documentId: docId,
                versionId: verId,
                cancellationToken: cancellationToken);
        }

        var docAuthority = new VerifiedAuthorityContext(
            ActorId: actorId,
            Capabilities: new[] { "DocumentSubmitter" },
            ScopeKind: "LeaseCase",
            ScopeTargetIdentifier: caseId.ToString("D"),
            ValidFrom: now.AddMinutes(-5),
            VerificationTime: now,
            ValidUntil: now.AddHours(2)
        );

        var cmd = new RegisterGovernedDocumentCommand(
            LeaseCaseId: caseId,
            LogicalCategory: logicalCategory,
            ActorId: actorId,
            AuthorityContext: docAuthority,
            ContentReceipt: receipt,
            GovernedDocumentId: docId,
            InitialVersionId: verId
        );

        var doc = await _registerDocHandler.HandleAsync(cmd, cancellationToken);
        return Ok(doc);
    }

    /// <summary>
    /// Re-trigger analysis execution on an existing document version without creating a new case.
    /// </summary>
    [HttpPost("{caseId:guid}/documents/{documentId:guid}/versions/{versionId:guid}/analyze")]
    [ProducesResponseType(typeof(DocumentAnalysisDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReanalyzeVersion(
        Guid caseId,
        Guid documentId,
        Guid versionId,
        [FromQuery] string? languageMode = "sin",
        [FromQuery] string? semanticProvider = "gemini",
        CancellationToken cancellationToken = default)
    {
        var leaseCase = await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(caseId), cancellationToken);
        if (!IsOfficer() && leaseCase.CreatedByActorId != ResolveActorId()) return StatusCode(403);
        var boundDocument = await _documentRepository.GetByIdAsync(new GovernedDocumentId(documentId), cancellationToken);
        if (boundDocument is null || boundDocument.LeaseCaseId.Value != caseId
            || !boundDocument.Versions.Any(v => v.Id.Value == versionId)) return NotFound();
        if (!IsOfficer() && !Application.Mappings.CaseInputDocuments.IsIntakeInput(boundDocument.LogicalCategory))
            return StatusCode(403);
        // 1. Check existing analysis
        DocumentAnalysisDto existingAnalysis;
        try
        {
            existingAnalysis = await _getAnalysisHandler.HandleAsync(
                new GetDocumentAnalysisByVersionIdQuery(versionId),
                cancellationToken);
        }
        catch (DocumentAnalysisNotFoundException)
        {
            // Initial request
            existingAnalysis = await _requestAnalysisHandler.HandleAsync(
                new RequestDocumentAnalysisCommand(
                    GovernedDocumentId: documentId,
                    DocumentVersionId: versionId,
                    ModelProvider: "PythonFastApi",
                    ModelName: semanticProvider ?? "gemini",
                    ModelVersion: "v1",
                    RequestedCapabilities: new[] { "Ocr", "SemanticExtraction" }
                ), cancellationToken);
        }

        // If existing is completed or failed, request a new run
        var targetRevision = existingAnalysis.Revision;
        Guid targetRunId;
        if (existingAnalysis.ActiveRun == null || existingAnalysis.ActiveRun.State != "Requested")
        {
            var updated = await _requestAnalysisHandler.HandleAsync(
                new RequestDocumentAnalysisCommand(
                    GovernedDocumentId: documentId,
                    DocumentVersionId: versionId,
                    ModelProvider: "PythonFastApi",
                    ModelName: semanticProvider ?? "gemini",
                    ModelVersion: "v1",
                    RequestedCapabilities: new[] { "Ocr", "SemanticExtraction" },
                    DocumentAnalysisId: existingAnalysis.Id,
                    ExpectedAnalysisRevision: existingAnalysis.Revision
                ), cancellationToken);

            targetRevision = updated.Revision;
            targetRunId = updated.ActiveRun!.Id;
        }
        else
        {
            targetRunId = existingAnalysis.ActiveRun.Id;
        }

        // Execute
        try
        {
            await _executionService.ExecuteAsync(
                documentAnalysisId: existingAnalysis.Id,
                analysisRunId: targetRunId,
                expectedAnalysisRevision: targetRevision,
                languageHint: languageMode,
                semanticProvider: semanticProvider,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Re-analysis execution encountered an exception for version {VersionId}.", versionId);
        }

        var finalAnalysis = await _getAnalysisHandler.HandleAsync(
            new GetDocumentAnalysisByVersionIdQuery(versionId),
            cancellationToken);

        return Ok(finalAnalysis);
    }

    /// <summary>
    /// Stream document binary content for viewing or downloading by authorized officers or case owners.
    /// </summary>
    [HttpGet("{caseId:guid}/documents/{documentId:guid}/versions/{versionId:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadDocumentVersion(
        Guid caseId,
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        LeaseCaseDto leaseCase;
        try
        {
            leaseCase = await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(caseId), cancellationToken);
        }
        catch (LeaseCaseNotFoundException)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }

        var actorId = ResolveActorId();
        var isOfficer = IsOfficer();
        if (!isOfficer && leaseCase.CreatedByActorId != actorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. You do not have permission to download documents for this case." });
        }

        var doc = await _documentRepository.GetByIdAsync(new GovernedDocumentId(documentId), cancellationToken);
        if (doc == null || doc.LeaseCaseId.Value != caseId)
        {
            return NotFound(new { error = $"Governed document '{documentId}' not found on case '{caseId}'." });
        }

        if (!isOfficer && !Application.Mappings.CaseInputDocuments.IsIntakeInput(doc.LogicalCategory))
            return StatusCode(StatusCodes.Status403Forbidden);

        var version = doc.Versions.FirstOrDefault(v => v.Id.Value == versionId);
        if (version == null)
        {
            return NotFound(new { error = $"Document version '{versionId}' not found on document '{documentId}'." });
        }

        var stream = await _contentReader.ReadContentAsync(
            doc.Id,
            version.Id,
            version.ContentReference.Value,
            cancellationToken);

        if (stream == null)
        {
            return NotFound(new { error = "Document content stream could not be located in storage." });
        }

        return File(stream, version.MediaType, version.OriginalFileName);
    }

    /// <summary>
    /// Authorized Officer verifies an extracted fact candidate.
    /// </summary>
    [HttpPost("{caseId:guid}/facts/verify")]
    [ProducesResponseType(typeof(HumanFactVerificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyCandidateFact(
        Guid caseId,
        [FromBody] VerifyFactApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Fact verification requires Officer role authority." });
        }

        var actorId = ResolveActorId();
        var now = DateTime.UtcNow;

        // Resolve current analysis aggregate to ensure valid run result id, revision, and GovernedDocumentId
        var analysis = await _getAnalysisByIdHandler.HandleAsync(
            new GetDocumentAnalysisByIdQuery(request.DocumentAnalysisId),
            cancellationToken);

        var sourceDocument = await _documentRepository.GetByIdAsync(new GovernedDocumentId(analysis.GovernedDocumentId), cancellationToken);
        if (sourceDocument?.LeaseCaseId.Value != caseId) return NotFound();
        if (sourceDocument.ActiveVersionId.Value != analysis.DocumentVersionId || analysis.ActiveRun?.State != "Completed"
            || analysis.ActiveRun.Result?.Id != request.AnalysisRunResultId)
            return Conflict(new { error = "The requested document version/run result is not current. Reload before verification." });

        FactValueDto? correctedValueDto = null;
        if (!string.IsNullOrWhiteSpace(request.CorrectedValue))
        {
            correctedValueDto = new FactValueDto(
                request.ValueKind ?? "Text",
                request.CorrectedValue.Trim());
        }

        // Normalize decision string to match Domain FactVerificationDecision enum
        var rawDecision = (request.Decision ?? "Confirmed").Trim();
        string decision;
        if (string.Equals(rawDecision, "Confirm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawDecision, "Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            decision = "Confirmed";
        }
        else if (string.Equals(rawDecision, "Correct", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawDecision, "Corrected", StringComparison.OrdinalIgnoreCase))
        {
            decision = "Corrected";
        }
        else
        {
            decision = "Unsupported";
        }

        var grantedAuthority = ExecutionAuthority(caseId, "FactVerifier");
        if (grantedAuthority is null || !User.HasClaim("authority_document_id", analysis.GovernedDocumentId.ToString("D")))
            return StatusCode(403, new { error = "An authenticated case/document-scoped FactVerifier assignment is required." });
        var authorityContext = grantedAuthority with { ScopeKind = "GovernedDocument",
            ScopeTargetIdentifier = analysis.GovernedDocumentId.ToString("D") };

        var runResultId = analysis.ActiveRun?.Result?.Id
            ?? (request.AnalysisRunResultId != Guid.Empty && request.AnalysisRunResultId != caseId
                ? request.AnalysisRunResultId
                : Guid.Empty);

        var expectedRevision = request.ExpectedAnalysisRevision > 0
            ? request.ExpectedAnalysisRevision
            : analysis.Revision;

        var sanitizedReason = string.Equals(decision, "Confirmed", StringComparison.OrdinalIgnoreCase)
            ? null
            : request.Reason;

        var cmd = new VerifyCandidateFactCommand(
            DocumentAnalysisId: request.DocumentAnalysisId,
            AnalysisRunResultId: runResultId,
            ExtractedFactId: request.ExtractedFactId,
            Decision: decision,
            CorrectedValue: correctedValueDto,
            Reason: sanitizedReason,
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: expectedRevision
        );

        var result = await _verifyFactHandler.HandleAsync(cmd, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Publishes an immutable verified fact snapshot and binds it to the lease case.
    /// </summary>
    [HttpPost("{caseId:guid}/snapshots")]
    [ProducesResponseType(typeof(VerifiedFactSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishSnapshot(
        Guid caseId,
        [FromBody] PublishSnapshotApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Publishing verified fact snapshots requires Officer role authority." });
        }

        var actorId = ResolveActorId();
        var now = DateTime.UtcNow;

        var analysis = await _getAnalysisByIdHandler.HandleAsync(
            new GetDocumentAnalysisByIdQuery(request.DocumentAnalysisId),
            cancellationToken);

        var sourceDocument = await _documentRepository.GetByIdAsync(new GovernedDocumentId(analysis.GovernedDocumentId), cancellationToken);
        if (sourceDocument?.LeaseCaseId.Value != caseId) return NotFound();
        if (sourceDocument.ActiveVersionId.Value != analysis.DocumentVersionId || analysis.ActiveRun?.State != "Completed"
            || analysis.ActiveRun.Result?.Id != request.AnalysisRunResultId)
            return Conflict(new { error = "The requested document version/run result is not current. Reload before publication." });

        var grantedAuthority = ExecutionAuthority(caseId, "FactSnapshotPublisher");
        if (grantedAuthority is null || !User.HasClaim("authority_document_id", analysis.GovernedDocumentId.ToString("D")))
            return StatusCode(403, new { error = "An authenticated case/document-scoped FactSnapshotPublisher assignment is required." });
        var authorityContext = grantedAuthority with { ScopeKind = "GovernedDocument",
            ScopeTargetIdentifier = analysis.GovernedDocumentId.ToString("D") };

        var runResultId = analysis.ActiveRun?.Result?.Id
            ?? (request.AnalysisRunResultId != Guid.Empty && request.AnalysisRunResultId != caseId
                ? request.AnalysisRunResultId
                : Guid.Empty);

        var expectedAnalysisRev = request.ExpectedAnalysisRevision > 0
            ? request.ExpectedAnalysisRevision
            : analysis.Revision;

        var createCmd = new CreateVerifiedFactSnapshotCommand(
            DocumentAnalysisId: request.DocumentAnalysisId,
            AnalysisRunResultId: runResultId,
            PublishingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: expectedAnalysisRev
        );

        var snapshotDto = await _createSnapshotHandler.HandleAsync(createCmd, cancellationToken);

        // Fetch fresh analysis to obtain updated revision for linking
        var updatedAnalysis = await _getAnalysisByIdHandler.HandleAsync(
            new GetDocumentAnalysisByIdQuery(request.DocumentAnalysisId),
            cancellationToken);

        // Bind snapshot to case
        var linkCmd = new LinkVerifiedFactSnapshotToCaseCommand(
            LeaseCaseId: caseId,
            DocumentAnalysisId: request.DocumentAnalysisId,
            SnapshotId: snapshotDto.Id,
            ExpectedAnalysisRevision: updatedAnalysis.Revision,
            ExpectedLeaseCaseRevision: request.ExpectedLeaseCaseRevision
        );

        await _linkSnapshotHandler.HandleAsync(linkCmd, cancellationToken);

        return Ok(snapshotDto);
    }

    /// <summary>
    /// Authenticated Applicant case milestone tracking endpoint.
    /// </summary>
    [HttpGet("tracking/{applicationReference}")]
    [ProducesResponseType(typeof(ApplicantCaseTrackingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrackApplication(
        string applicationReference,
        CancellationToken cancellationToken = default)
    {
        var actorId = ResolveActorId();

        var tracking = await _trackingHandler.HandleAsync(
            new GetApplicantCaseTrackingQuery(applicationReference, actorId),
            cancellationToken);

        if (tracking is null)
        {
            return NotFound(new { error = $"Application reference '{applicationReference}' not found or access denied." });
        }

        return Ok(tracking);
    }

    /// <summary>
    /// Evaluates Check A: Required Supporting Document Completeness against governed configuration requirement set.
    /// </summary>
    [HttpPost("{caseId:guid}/completeness/documents")]
    [ProducesResponseType(typeof(DocumentCompletenessAssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssessDocumentCompleteness(
        Guid caseId,
        [FromBody] AssessDocumentCompletenessApiRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Assessing document completeness requires Officer authority." });
        }

        LeaseCaseDto leaseCase;
        try
        {
            leaseCase = await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(caseId), cancellationToken);
        }
        catch (LeaseCaseNotFoundException)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }

        var docs = (await _getDocsHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(caseId), cancellationToken))
            .Where(d => Application.Mappings.CaseInputDocuments.IsIntakeInput(d.LogicalCategory)).ToList();
        if (docs.Count == 0)
        {
            return BadRequest(new { error = "Cannot assess completeness: Case has no uploaded documents." });
        }

        var reqSetId = request?.RequirementSetIdentifier ?? "DOC-REQ-AGRI-V1";
        var reqSetVer = request?.RequirementSetVersion ?? "1.0";

        var assessedBindings = new List<AssessedDocumentBindingDto>();
        var classifiedDocs = new List<ClassifiedDocumentDto>();
        var correctionRecords = (await _correctionRepository.ListAsync(caseId, cancellationToken)).Where(r => r.Purpose == "Correction").ToList();

        foreach (var doc in docs)
        {
            var activeVer = doc.Versions.FirstOrDefault(v => v.Id == doc.ActiveVersionId) ?? doc.Versions.Last();
            var binding = new AssessedDocumentBindingDto(
                GovernedDocumentId: doc.Id,
                DocumentVersionId: activeVer.Id,
                ChecksumAlgorithm: activeVer.ChecksumAlgorithm,
                ChecksumValue: activeVer.ChecksumValue
            );
            assessedBindings.Add(binding);

            // Infer classification code from LogicalCategory or latest DocumentAnalysis
            string classCode = doc.LogicalCategory;
            if (string.Equals(classCode, "Proposal", StringComparison.OrdinalIgnoreCase))
            {
                classCode = "Proposal";
            }
            else if (classCode.Contains("Survey", StringComparison.OrdinalIgnoreCase))
            {
                classCode = "SurveyPlan";
            }
            else if (classCode.Contains("Identity", StringComparison.OrdinalIgnoreCase) || classCode.Contains("NIC", StringComparison.OrdinalIgnoreCase))
            {
                classCode = "IdentityProof";
            }
            else if (classCode.Contains("Deed", StringComparison.OrdinalIgnoreCase) || classCode.Contains("Title", StringComparison.OrdinalIgnoreCase))
            {
                classCode = "Deed";
            }
            else if (classCode.Contains("Business", StringComparison.OrdinalIgnoreCase) || classCode.Contains("Plan", StringComparison.OrdinalIgnoreCase))
            {
                classCode = "BusinessPlan";
            }

            classifiedDocs.Add(new ClassifiedDocumentDto(
                Id: Guid.NewGuid(),
                DocumentBinding: binding,
                OriginalClassificationCode: classCode,
                ConfidenceScore: 0m,
                Status: correctionRecords.Any(r => r.Status == "AcceptedAwaitingReassessment"
                    && r.Submissions.LastOrDefault() is { } submission
                    && submission.DocumentId == doc.Id && submission.VersionId == activeVer.Id)
                    ? "Accepted" : "Uncertain",
                ClassifiedAt: activeVer.SubmittedAt
            ));
        }

        var cmd = new AssessDocumentCompletenessCommand(
            LeaseCaseId: caseId,
            RequirementSetIdentifier: reqSetId,
            RequirementSetVersion: reqSetVer,
            AssessedDocuments: assessedBindings,
            ClassifiedDocuments: classifiedDocs
        );

        try
        {
            var result = await _assessDocCompletenessHandler.HandleAsync(cmd, cancellationToken);
            return Ok(result);
        }
        catch (DocumentRequirementSetNotFoundException)
        {
            return Conflict(new { error = "Requirements not configured", detail = "Requirements not configured", requirementSetIdentifier = reqSetId, requirementSetVersion = reqSetVer });
        }
    }

    /// <summary>
    /// Evaluates Check B: Required Proposal Content & Sections against governed proposal template.
    /// </summary>
    [HttpPost("{caseId:guid}/completeness/proposal")]
    [ProducesResponseType(typeof(ProposalContentAssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssessProposalContent(
        Guid caseId,
        [FromBody] AssessProposalContentApiRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Assessing proposal content requires Officer authority." });
        }

        LeaseCaseDto leaseCase;
        try
        {
            leaseCase = await _getCaseHandler.HandleAsync(new GetLeaseCaseByIdQuery(caseId), cancellationToken);
        }
        catch (LeaseCaseNotFoundException)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }

        var docs = await _getDocsHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(caseId), cancellationToken);
        var proposals = docs.Where(d => string.Equals(d.LogicalCategory, "Proposal", StringComparison.OrdinalIgnoreCase)).ToList();
        var proposalDoc = request?.ProposalDocumentId is { } selectedId ? proposals.SingleOrDefault(d => d.Id == selectedId)
            : proposals.Count == 1 ? proposals[0] : null;
        if (proposalDoc is null && proposals.Count > 1) return Conflict(new { error = "Select the Proposal document explicitly before assessing its content." });

        if (proposalDoc == null)
        {
            return BadRequest(new { error = "Cannot assess proposal content: Case has no proposal document." });
        }

        var activeVer = proposalDoc.Versions.FirstOrDefault(v => v.Id == proposalDoc.ActiveVersionId) ?? proposalDoc.Versions.Last();

        var templateId = request?.TemplateId ?? "STD-PROP-2026-V1";
        var templateVer = request?.TemplateVersion ?? "1.0";

        var template = await _proposalTemplateProvider.GetTemplateAsync(
            new StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.ProposalTemplateId(templateId),
            templateVer,
            cancellationToken);

        if (template == null)
        {
            return Conflict(new { error = "Requirements not configured", detail = "Requirements not configured", templateId, templateVersion = templateVer });
        }

        // Retrieve DocumentAnalysis for this version to extract observations
        DocumentAnalysisDto? analysis = null;
        try
        {
            analysis = await _getAnalysisHandler.HandleAsync(
                new GetDocumentAnalysisByVersionIdQuery(activeVer.Id),
                cancellationToken);
        }
        catch (DocumentAnalysisNotFoundException)
        {
            // Analysis not found or not run yet
        }

        var observations = analysis != null && _templateMapping != null
            ? await _templateMapping.MapAsync(analysis, template, cancellationToken)
            : template.Requirements.Select(r => new ProposalObservationDto(r.Id.Value, "Uncertain", null,
                null, null, analysis?.ActiveRun?.Id.ToString("D"),
                "Template mapping is unavailable; extraction absence does not establish missing content.")).ToArray();

        // Network inference cannot authorize an assessment of a superseded document/run.
        var currentDocs = await _getDocsHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(caseId), cancellationToken);
        if (currentDocs.SingleOrDefault(d => d.Id == proposalDoc.Id)?.ActiveVersionId != activeVer.Id)
            return Conflict(new { error = "Proposal version changed during template assessment." });
        if (analysis != null)
        {
            var currentAnalysis = await _getAnalysisHandler.HandleAsync(new GetDocumentAnalysisByVersionIdQuery(activeVer.Id), cancellationToken);
            if (currentAnalysis.ActiveRun?.Id != analysis.ActiveRun?.Id || currentAnalysis.ActiveRun?.State != "Completed")
                return Conflict(new { error = "Proposal analysis changed during template assessment." });
        }

        var cmd = new AssessProposalContentCommand(
            LeaseCaseId: caseId,
            ProposalDocumentId: proposalDoc.Id,
            DocumentVersionId: activeVer.Id,
            ChecksumAlgorithm: activeVer.ChecksumAlgorithm,
            ChecksumValue: activeVer.ChecksumValue,
            TemplateId: templateId,
            TemplateVersion: templateVer,
            ExtractionReference: analysis?.ActiveRun?.Id.ToString("D"),
            Observations: observations,
            ExpectedRevision: leaseCase.Revision
        );

        var result = await _assessPropContentHandler.HandleAsync(cmd, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets current dual completeness state, checklist findings, staleness, and active correction requests.
    /// </summary>
    [HttpGet("{caseId:guid}/completeness")]
    [ProducesResponseType(typeof(CaseCompletenessSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCompletenessSummary(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var domainCase = await _leaseCaseRepository.GetByIdAsync(
            new StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCaseId(caseId),
            cancellationToken);

        if (domainCase == null)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }
        if (!IsOfficer() && domainCase.CreatedByActorId != ResolveActorId())
            return StatusCode(StatusCodes.Status403Forbidden);

        // 1. Get latest document completeness assessment
        var docCompleteness = await _getLatestDocCompletenessHandler.HandleAsync(
            new GetLatestDocumentCompletenessAssessmentByCaseIdQuery(caseId),
            cancellationToken);

        // 2. Get active proposal document and check staleness
        var docs = (await _getDocsHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(caseId), cancellationToken))
            .Where(d => Application.Mappings.CaseInputDocuments.IsIntakeInput(d.LogicalCategory)).ToList();
        var assessedProposalId = domainCase.GetLatestRecordedProposalContentAssessment()?.SourceBinding.ProposalDocumentId.Value;
        var proposalDoc = docs.SingleOrDefault(d => d.Id == assessedProposalId
            && string.Equals(d.LogicalCategory, "Proposal", StringComparison.OrdinalIgnoreCase));

        bool isDocCompletenessCurrent = false;
        if (docCompleteness != null && docs.Count > 0)
        {
            // Current if all currently active versions are assessed and no unassessed documents exist
            var activeDocVersionIds = docs.Select(d => d.ActiveVersionId).ToHashSet();
            var assessedVersionIds = docCompleteness.AssessedDocuments.Select(a => a.DocumentVersionId).ToHashSet();
            isDocCompletenessCurrent = activeDocVersionIds.SetEquals(assessedVersionIds);
        }

        // 3. Get latest proposal content assessment and verify staleness against active proposal document version
        ProposalContentAssessmentDto? propContent = null;
        bool isPropContentCurrent = false;

        var latestRecordedProp = domainCase.GetLatestRecordedProposalContentAssessment();
        if (latestRecordedProp != null)
        {
            propContent = StateLandGovernance.WorkflowGovernance.Application.Mappings.ProposalContentMapper.ToDto(latestRecordedProp);

            if (proposalDoc != null)
            {
                var activeVer = proposalDoc.Versions.FirstOrDefault(v => v.Id == proposalDoc.ActiveVersionId) ?? proposalDoc.Versions.Last();
                var currentBinding = new StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.ProposalSourceBinding(
                    domainCase.Id,
                    new GovernedDocumentId(proposalDoc.Id),
                    new DocumentVersionId(activeVer.Id),
                    new DocumentChecksum(activeVer.ChecksumAlgorithm, activeVer.ChecksumValue),
                    latestRecordedProp.SourceBinding.TemplateId,
                    latestRecordedProp.SourceBinding.TemplateVersion,
                    latestRecordedProp.SourceBinding.AssessedAt,
                    latestRecordedProp.SourceBinding.ExtractionReference
                );

                isPropContentCurrent = domainCase.IsAssessmentCurrent(
                    latestRecordedProp.Id,
                    currentBinding,
                    latestRecordedProp.TemplateSnapshot.DefinitionDigest
                );
            }
        }

        var correctionRecords = (await _correctionRepository.ListAsync(caseId, cancellationToken)).Where(r => r.Purpose == "Correction").ToList();
        var activeCorrections = correctionRecords.Select(ToCorrectionDto).ToList();
        var documentRequirementsConfigured = await _documentRequirementProvider.GetRequirementSetAsync(
            docCompleteness?.RequirementSetIdentifier ?? "DOC-REQ-AGRI-V1",
            docCompleteness?.RequirementSetVersion ?? "1.0", cancellationToken) is not null;
        var proposalTemplateConfigured = await _proposalTemplateProvider.GetTemplateAsync(
            latestRecordedProp?.SourceBinding.TemplateId ?? new StateLandGovernance.WorkflowGovernance.Domain.ProposalContent.ProposalTemplateId("STD-PROP-2026-V1"),
            latestRecordedProp?.SourceBinding.TemplateVersion ?? "1.0", cancellationToken) is not null;
        isDocCompletenessCurrent &= documentRequirementsConfigured && !correctionRecords.Any(r =>
            r.Submissions.Any(s => s.Decision == "Accepted" && s.ReviewedAtUtc > docCompleteness?.AssessedAt));
        isPropContentCurrent &= proposalTemplateConfigured;

        return Ok(new CaseCompletenessSummaryResponse(
            LeaseCaseId: caseId,
            DocumentCompleteness: docCompleteness,
            ProposalContent: propContent,
            IsDocumentCompletenessCurrent: isDocCompletenessCurrent,
            IsProposalContentCurrent: isPropContentCurrent,
            ActiveCorrectionRequests: activeCorrections,
            DocumentRequirementsConfigured: documentRequirementsConfigured,
            ProposalTemplateConfigured: proposalTemplateConfigured
        ));
    }

    /// <summary>
    /// Creates a formal administrative correction request on the case identifying required documents or revisions.
    /// </summary>
    [HttpPost("{caseId:guid}/corrections")]
    [ProducesResponseType(typeof(CaseCorrectionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCorrectionRequest(
        Guid caseId,
        [FromBody] CreateCorrectionApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsOfficer())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Creating correction requests requires Officer authority." });
        }

        var domainCase = await _leaseCaseRepository.GetByIdAsync(
            new StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCaseId(caseId),
            cancellationToken);

        if (domainCase == null)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }

        var codeStr = request.RequiredClassificationCode ?? "SupportingDocument";
        var classificationCode = new StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness.DocumentClassificationCode(codeStr);
        if (request.DueDateUtc is null)
            return BadRequest(new { error = "An explicit officer-selected due date is required." });
        var record = await _corrections.CreateAsync(caseId, ResolveActorId(), request.RequiredAction,
            classificationCode.Value, request.RequiredSectionId, request.OfficerInstructions,
            request.DueDateUtc.Value, cancellationToken);
        return Ok(ToCorrectionDto(record));
    }

    /// <summary>
    /// Applicant responds to a correction request with an uploaded supporting document.
    /// </summary>
    [HttpPost("{caseId:guid}/corrections/{requestId:guid}/respond")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CaseCorrectionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RespondToCorrection(
        Guid caseId,
        Guid requestId,
        [FromForm] IFormFile? file,
        [FromForm] string? applicantResponseNotes = null,
        CancellationToken cancellationToken = default,
        [FromForm] int expectedRevision = 0)
    {
        var domainCase = await _leaseCaseRepository.GetByIdAsync(
            new StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCaseId(caseId),
            cancellationToken);

        if (domainCase == null)
        {
            return NotFound(new { error = $"Lease case '{caseId}' not found." });
        }

        var isOfficer = IsOfficer();
        var actorId = ResolveActorId();
        if (domainCase.CreatedByActorId != actorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Access denied. Applicants may only respond to corrections on their own cases." });
        }

        var requirement = domainCase.DocumentSubmissionRequirements.FirstOrDefault(r => r.Id == requestId);
        if (requirement == null)
        {
            return NotFound(new { error = $"Correction request '{requestId}' not found." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "A document file must be uploaded to satisfy the correction request." });
        }

        var correction = await _correctionRepository.GetAsync(caseId, requestId, cancellationToken);
        if (correction is null) return NotFound(new { error = "Correction not found." });
        if (correction.Revision != expectedRevision || correction.Status == "AcceptedAwaitingReassessment")
            return Conflict(new { error = "Correction revision conflict or already reviewed. Reload before retrying." });
        var proposalCandidates = correction.RequiredAction == "ReviseProposal"
            ? (await _getDocsHandler.HandleAsync(new GetDocumentsByLeaseCaseIdQuery(caseId), cancellationToken))
                .Where(d => string.Equals(d.LogicalCategory, "Proposal", StringComparison.OrdinalIgnoreCase)).ToList()
            : new List<GovernedDocumentDto>();
        if (proposalCandidates.Count > 1) return Conflict(new { error = "Multiple Proposals exist; a document-bound correction is required before revision. No proposal was chosen automatically." });
        var proposal = proposalCandidates.SingleOrDefault();
        if (correction.RequiredAction == "ReviseProposal" && proposal is null)
            return BadRequest(new { error = "The existing proposal document is required for revision." });

        // Upload and register document
        var docId = proposal?.Id ?? Guid.NewGuid();
        var verId = Guid.NewGuid();
        DocumentContentReceipt receipt;
        using (var stream = file.OpenReadStream())
        {
            receipt = await _contentWriter.WriteAsync(
                content: stream,
                originalFileName: file.FileName,
                mediaType: file.ContentType ?? "application/pdf",
                documentId: docId,
                versionId: verId,
                cancellationToken: cancellationToken
            );
        }

        var now = DateTime.UtcNow;
        var docAuthority = new VerifiedAuthorityContext(
            ActorId: actorId,
            Capabilities: new[] { "DocumentSubmitter" },
            ScopeKind: "LeaseCase",
            ScopeTargetIdentifier: domainCase.Id.Value.ToString("D"),
            ValidFrom: now.AddMinutes(-5),
            VerificationTime: now,
            ValidUntil: now.AddHours(2)
        );

        var registerDocCmd = new RegisterGovernedDocumentCommand(
            LeaseCaseId: domainCase.Id.Value,
            LogicalCategory: requirement.DocumentClassificationCode?.Value ?? "SupportingDocument",
            ActorId: actorId,
            AuthorityContext: docAuthority,
            ContentReceipt: receipt,
            GovernedDocumentId: docId,
            InitialVersionId: verId
        );

        async Task<GovernedDocumentDto> RegisterDocument() => proposal is null
            ? await _registerDocHandler.HandleAsync(registerDocCmd, cancellationToken)
            : await _addVersionHandler.HandleAsync(new AddDocumentVersionCommand(proposal.Id,
                proposal.ActiveVersionId, receipt, actorId, docAuthority, proposal.Revision, verId), cancellationToken);
        var record = await _corrections.RegisterAndSubmitAsync(caseId, requestId, actorId,
            RegisterDocument, applicantResponseNotes, expectedRevision, cancellationToken);
        return Ok(ToCorrectionDto(record));
    }

    [HttpPost("{caseId:guid}/corrections/{requestId:guid}/review")]
    public async Task<IActionResult> ReviewCorrection(Guid caseId, Guid requestId,
        [FromBody] ReviewCorrectionApiRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsOfficer()) return StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            var record = await _corrections.ReviewAsync(caseId, requestId, ResolveActorId(),
                request.Accept, request.Reason, request.ExpectedRevision, cancellationToken);
            return Ok(ToCorrectionDto(record));
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private static CaseCorrectionRequestDto ToCorrectionDto(CorrectionRecord record)
    {
        var submission = record.Submissions.LastOrDefault();
        return new(record.Id, record.LeaseCaseId, "Officer", record.CreatedAtUtc, record.DueDateUtc,
            record.Status, record.RequiredAction, record.ClassificationCode, record.SectionId,
            record.Instructions, submission?.DocumentId, submission?.SubmittedAtUtc, record.Revision);
    }

    private Guid ResolveActorId()
    {
        var subClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier);

        // Development-only fallback: only allow X-Actor-Id header if running in Development environment
        if (string.IsNullOrWhiteSpace(subClaim) && User?.Identity?.IsAuthenticated != true && IsLocalDevelopmentRequest())
        {
            subClaim = Request?.Headers["X-Actor-Id"].FirstOrDefault();
        }

        if (Guid.TryParse(subClaim, out var actorId) && actorId != Guid.Empty)
        {
            return actorId;
        }

        throw new UnauthorizedAccessException("An authenticated actor identity is required.");
    }

    private bool IsOfficer()
    {
        if (User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(ControllerContext.RouteData?.Values["caseId"]?.ToString(), out var requestedCase) &&
            ExecutionAuthority(requestedCase, "CaseWorkspaceReviewer") == null) return false;
        if (User.HasClaim(c => c.Type == "authority_managed_case") &&
            Guid.TryParse(ControllerContext.RouteData?.Values["caseId"]?.ToString(), out var managedCase) &&
            ExecutionAuthority(managedCase, "CaseWorkspaceReviewer") == null) return false;
        if (User.HasClaim(c => c.Type == "development_account") &&
            Guid.TryParse(ControllerContext.RouteData?.Values["caseId"]?.ToString(), out var developmentCase) &&
            !User.HasClaim("authority_case_id", developmentCase.ToString("D"))) return false;
        // An institutional reviewer receives only the scoped request/evidence projection unless
        // identity issuance separately grants access to this complete officer workspace.
        if (User?.Identity?.IsAuthenticated == true && User.HasClaim(c => c.Type == "institution"))
        {
            var routeCase = ControllerContext.RouteData?.Values["caseId"]?.ToString();
            if (!Guid.TryParse(routeCase, out var scopedCase) || ExecutionAuthority(scopedCase, "CaseWorkspaceReviewer") == null)
                return false;
        }
        if (User?.IsInRole("Officer") == true || User?.HasClaim(ClaimTypes.Role, "Officer") == true ||
            User?.IsInRole("ComplianceOfficer") == true || User?.HasClaim(ClaimTypes.Role, "ComplianceOfficer") == true ||
            User?.IsInRole("LandOfficer") == true || User?.HasClaim(ClaimTypes.Role, "LandOfficer") == true)
        {
            return true;
        }

        // Development-only fallback: only allow X-Actor-Role header if running in Development environment
        if (User?.Identity?.IsAuthenticated != true && IsLocalDevelopmentRequest())
        {
            var headerRole = Request?.Headers["X-Actor-Role"].FirstOrDefault();
            return string.Equals(headerRole, "Officer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(headerRole, "ComplianceOfficer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(headerRole, "LandOfficer", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool IsLocalDevelopmentRequest()
    {
        var remote = HttpContext.Connection.RemoteIpAddress;
        if (remote?.IsIPv4MappedToIPv6 == true) remote = remote.MapToIPv4();
        return _environment.IsDevelopment() && remote != null && System.Net.IPAddress.IsLoopback(remote);
    }

    private async Task<IReadOnlyList<CaseGovernedFieldRowDto>> Build17FieldInventoryAsync(
        DocumentAnalysisDto? analysis,
        CancellationToken cancellationToken)
    {
        var extractedFacts = analysis?.ActiveRun?.Result?.ExtractedCandidateFacts
            ?? (IReadOnlyList<CandidateFactDto>)Array.Empty<CandidateFactDto>();

        var verifications = analysis?.Verifications.Where(v => v.AnalysisRunId == analysis.ActiveRun?.Id).ToList()
            ?? (IReadOnlyList<HumanFactVerificationDto>)Array.Empty<HumanFactVerificationDto>();

        var snapshotEntries = analysis?.VerifiedFactSnapshots.OrderBy(s => s.PublishedAt).LastOrDefault(s => s.AnalysisRunId == analysis.ActiveRun?.Id)?.Entries
            ?? (IReadOnlyList<VerifiedFactEntryDto>)Array.Empty<VerifiedFactEntryDto>();

        var activeRunState = analysis?.ActiveRun?.State;

        SemanticArtifactPayload? rawArtifactPayload = null;
        if (analysis?.ActiveRun != null && _artifactReader != null)
        {
            try
            {
                var storageKey = $"analysis-artifacts/{analysis.DocumentVersionId:D}/{analysis.ActiveRun!.Id:D}/semantic-extraction.json";
                var rawBytes = await _artifactReader.ReadAsync(storageKey, cancellationToken);
                if (rawBytes.HasValue && rawBytes.Value.Length > 0)
                {
                    rawArtifactPayload = JsonSerializer.Deserialize<SemanticArtifactPayload>(
                        rawBytes.Value.Span,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read or parse semantic extraction artifact for version {VersionId}", analysis.DocumentVersionId);
            }
        }

        var list = new List<CaseGovernedFieldRowDto>();

        foreach (var (code, name) in Canonical17Fields)
        {
            var matchFact = extractedFacts.FirstOrDefault(f => string.Equals(f.FactCode, code, StringComparison.OrdinalIgnoreCase));
            var verification = matchFact != null
                ? verifications.FirstOrDefault(v => v.ExtractedFactId == matchFact.Id)
                : null;
            var snapshotEntry = snapshotEntries.FirstOrDefault(e => string.Equals(e.FactCode, code, StringComparison.OrdinalIgnoreCase));

            // Check for ungrounded candidate or inventory record in the raw response artifact
            var rawCandidate = rawArtifactPayload?.AllCandidates?.FirstOrDefault(
                c => string.Equals(c.FieldCode, code, StringComparison.OrdinalIgnoreCase));
            var rawInventoryItem = rawArtifactPayload?.FieldInventory?.FirstOrDefault(
                fi => string.Equals(fi.FieldCode, code, StringComparison.OrdinalIgnoreCase));

            string status;
            string? candidateVal = matchFact?.CandidateValue;
            string? preferredVal = null;
            decimal? confidenceScore = matchFact?.ConfidenceScore;
            int? pageNumber = matchFact?.PageNumber;
            string? evidenceQuote = matchFact?.Excerpt;
            Guid? extractedFactId = matchFact?.Id;
            bool isGrounded = true;

            if (snapshotEntry != null)
            {
                status = "Verified";
                preferredVal = snapshotEntry.EffectiveValue?.CanonicalValue;
                if (snapshotEntry.HumanEvidence is { } human)
                {
                    pageNumber = human.PageNumber;
                    evidenceQuote = human.Quote;
                    confidenceScore = null;
                    extractedFactId = null;
                }
            }
            else if (verification != null)
            {
                status = verification.Decision; // Confirmed, Corrected, Unsupported
                preferredVal = verification.CorrectedValue?.CanonicalValue ?? verification.OriginalMachineValue?.CanonicalValue;
            }
            else if (matchFact != null)
            {
                status = "Extracted";
                preferredVal = matchFact.CandidateValue;
            }
            else if (rawCandidate != null && !rawCandidate.IsEvidenceVerified)
            {
                // Ungrounded Candidate: Extracted by LLM, but citation failed strict character-level match against OCR text
                status = "Ungrounded";
                candidateVal = rawCandidate.CanonicalValue;
                preferredVal = rawCandidate.CanonicalValue;
                confidenceScore = rawCandidate.Confidence.HasValue ? (decimal)Math.Clamp(rawCandidate.Confidence.Value, 0.0, 1.0) : null;
                pageNumber = rawCandidate.Evidence?.PageNumber;
                evidenceQuote = rawCandidate.Evidence?.Quote;
                extractedFactId = null;
                isGrounded = false;
            }
            else if (rawInventoryItem != null && (rawInventoryItem.CandidateCount == 0 || string.Equals(rawInventoryItem.Status, "Missing", StringComparison.OrdinalIgnoreCase)))
            {
                // Machine completed analysis and found zero mentions for this extraction field
                status = "NoCandidate";
            }
            else if (activeRunState == "Completed")
            {
                status = "NoCandidate";
            }
            else
            {
                status = "Unavailable";
            }

            list.Add(new CaseGovernedFieldRowDto(
                FactCode: code,
                DisplayName: name,
                Status: status,
                CandidateValue: candidateVal,
                PreferredValue: preferredVal,
                ConfidenceScore: confidenceScore,
                PageNumber: pageNumber,
                EvidenceQuote: evidenceQuote,
                ExtractedFactId: extractedFactId,
                VerificationId: verification?.Id,
                OfficerDecision: verification?.Decision,
                OfficerReason: verification?.Reason,
                IsGrounded: isGrounded,
                SourceKind: snapshotEntry?.SourceKind,
                HumanEvidenceObservationId: snapshotEntry?.HumanEvidence?.ObservationId
            ));
        }

        return list;
    }

    private sealed record SemanticArtifactPayload(
        [property: JsonPropertyName("all_candidates")] IReadOnlyList<SemanticArtifactCandidate>? AllCandidates,
        [property: JsonPropertyName("field_inventory")] IReadOnlyList<SemanticArtifactFieldInventoryItem>? FieldInventory,
        [property: JsonPropertyName("candidate_count")] int? CandidateCount
    );

    private sealed record SemanticArtifactCandidate(
        [property: JsonPropertyName("field_code")] string FieldCode,
        [property: JsonPropertyName("canonical_value")] string? CanonicalValue,
        [property: JsonPropertyName("confidence")] double? Confidence,
        [property: JsonPropertyName("evidence")] SemanticArtifactEvidence? Evidence,
        [property: JsonPropertyName("is_evidence_verified")] bool IsEvidenceVerified
    );

    private sealed record SemanticArtifactEvidence(
        [property: JsonPropertyName("page_number")] int? PageNumber,
        [property: JsonPropertyName("quote")] string? Quote
    );

    private sealed record SemanticArtifactFieldInventoryItem(
        [property: JsonPropertyName("field_code")] string FieldCode,
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("preferred_value")] string? PreferredValue,
        [property: JsonPropertyName("page_number")] int? PageNumber,
        [property: JsonPropertyName("confidence")] double? Confidence,
        [property: JsonPropertyName("is_grounded")] bool? IsGrounded,
        [property: JsonPropertyName("candidate_count")] int? CandidateCount
    );
}
