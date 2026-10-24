namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class GetApplicantCaseTrackingQueryHandler : IQueryHandler<GetApplicantCaseTrackingQuery, ApplicantCaseTrackingDto?>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly ICorrectionRepository _corrections;
    private readonly IWorkflowExecutionStore? _executions;

    public GetApplicantCaseTrackingQueryHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IGovernedDocumentRepository documentRepository,
        IDocumentAnalysisRepository analysisRepository, ICorrectionRepository corrections, IWorkflowExecutionStore? executions = null)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _corrections = corrections;
        _executions = executions;
    }

    public async Task<ApplicantCaseTrackingDto?> HandleAsync(
        GetApplicantCaseTrackingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.ApplicationReference);

        var leaseCase = await _leaseCaseRepository.GetByApplicationReferenceAsync(
            query.ApplicationReference.Trim(),
            cancellationToken);

        if (leaseCase is null)
        {
            return null;
        }

        // Enforce owner authorization: applicants can ONLY track their own cases
        if (query.OwnerActorId != Guid.Empty && leaseCase.CreatedByActorId != query.OwnerActorId)
        {
            return null; // Return null so unauthorized callers get 404 (prevent case existence probing)
        }

        var docs = (await _documentRepository.GetByLeaseCaseIdAsync(leaseCase.Id, cancellationToken))
            .Where(d => Application.Mappings.CaseInputDocuments.IsIntakeInput(d.LogicalCategory)).ToList();
        var currentAnalyses = new Dictionary<Guid, Domain.DocumentAnalysis.DocumentAnalysis>();

        // Derive truthful document statuses
        var docStatuses = new List<ApplicantDocumentStatusDto>();
        foreach (var doc in docs)
        {
            var latestVersion = doc.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            var statusStr = "Uploaded";
            if (latestVersion != null)
            {
                var analysis = await _analysisRepository.GetByDocumentVersionIdAsync(latestVersion.Id, cancellationToken);
                if (analysis != null)
                {
                    currentAnalyses[doc.Id.Value] = analysis;
                    var activeRun = analysis.Runs.OrderByDescending(r => r.RunNumber).FirstOrDefault();
                    if (activeRun != null)
                    {
                        statusStr = activeRun.State switch
                        {
                            Domain.DocumentAnalysis.AnalysisRunState.Requested => "QueuedForExtraction",
                            Domain.DocumentAnalysis.AnalysisRunState.Running => "Processing",
                            Domain.DocumentAnalysis.AnalysisRunState.Completed => "DataExtracted",
                            Domain.DocumentAnalysis.AnalysisRunState.Failed => "ProcessingFailed",
                            _ => "Uploaded"
                        };
                    }

                    if (activeRun?.State == Domain.DocumentAnalysis.AnalysisRunState.Completed &&
                        analysis.VerifiedFactSnapshots.Any(s => s.AnalysisRunId == activeRun.Id))
                    {
                        statusStr = "Verified";
                    }
                }
            }

            var activeVer = doc.Versions.FirstOrDefault(v => v.Id == doc.ActiveVersionId) ?? doc.Versions.LastOrDefault();
            docStatuses.Add(new ApplicantDocumentStatusDto(
                DocumentId: doc.Id.Value,
                LogicalCategory: doc.LogicalCategory,
                OriginalFileName: activeVer?.OriginalFileName ?? "Document",
                ProcessingStatus: statusStr,
                UploadedAt: activeVer?.SubmittedAt ?? leaseCase.CreatedAt
            ));
        }

        // Truthfully derive milestones from persisted domain state
        var linkedSnapshot = leaseCase.CurrentVerifiedFactSnapshotId == Guid.Empty ? null
            : await _analysisRepository.GetSnapshotByIdAsync(new(leaseCase.CurrentVerifiedFactSnapshotId), cancellationToken);
        var snapshotDocument = docs.SingleOrDefault(d => d.Id == linkedSnapshot?.GovernedDocumentId);
        var snapshotAnalysis = snapshotDocument != null && currentAnalyses.TryGetValue(snapshotDocument.Id.Value, out var selected) ? selected : null;
        var currentRun = snapshotAnalysis?.Runs.OrderByDescending(r => r.RunNumber).FirstOrDefault();
        var hasSnapshot = linkedSnapshot != null && snapshotDocument?.ActiveVersionId == linkedSnapshot.DocumentVersionId &&
            currentRun?.State == Domain.DocumentAnalysis.AnalysisRunState.Completed && currentRun.Id == linkedSnapshot.AnalysisRunId;
        var inWorkflow = leaseCase.Status == LeaseCaseStatus.InWorkflow;
        var isApproved = leaseCase.Status == LeaseCaseStatus.ApprovedWithConditions
            || leaseCase.Status == LeaseCaseStatus.ReadyForHandoff
            || leaseCase.Status == LeaseCaseStatus.HandedOff;

        var m1State = "Completed";
        var m2State = docStatuses.Any(d => d.ProcessingStatus == "Processing" || d.ProcessingStatus == "QueuedForExtraction")
            ? "InProgress"
            : (docStatuses.Any(d => d.ProcessingStatus == "DataExtracted" || d.ProcessingStatus == "Verified") ? "Completed" : "InProgress");

        var m3State = hasSnapshot ? "Completed" : (m2State == "Completed" ? "InProgress" : "Pending");
        var execution = _executions == null ? null : await _executions.GetAsync(leaseCase.Id.Value, cancellationToken);
        var m4State = execution?.Status == Domain.WorkflowExecution.WorkflowExecutionStatus.Completed || isApproved
            ? "Completed" : execution != null ? "InProgress" : "Pending";

        var milestones = new List<ApplicantMilestoneDto>
        {
            new("SUBMITTED", "Application Intake", "Proposal and initial identity intake submitted.", m1State, leaseCase.CreatedAt),
            new("INTEL_PROCESSING", "Automated Document Ingestion", "Optical character recognition and semantic data extraction.", m2State, null),
            new("OFFICER_VERIFICATION", "Administrative Fact Verification", "Authorized land officers verify facts and establish immutable record.", m3State, null),
            new("GOVERNANCE_WORKFLOW", "Institutional Review", "Progress reflects persisted configured project reviews; completion does not imply approval.", m4State, null),
        };
        // Only persisted plan state produces this applicant milestone. Internal routing/findings stay officer-only.
        var plan = leaseCase.ActiveWorkflowPlan;
        milestones.Insert(3, new ApplicantMilestoneDto("WORKFLOW_PLAN", "Workflow plan review",
            "An officer reviews the proposed institutional review blueprint; plan approval is not application approval.",
            plan?.Status == Domain.WorkflowPlanning.WorkflowPlanStatus.Approved ? "Completed" : plan is not null ? "InProgress" : "Pending",
            plan?.ApprovedAt));

        var completedCount = milestones.Count(m => m.State == "Completed");
        var currentMilestone = milestones.FirstOrDefault(m => m.State == "InProgress")?.Title
            ?? (completedCount == milestones.Count ? "Completed" : "Under Review");

        // Map document submission requirements to applicant-facing correction notices
        // Excludes internal administrative notes and sensitive officer comments
        var correctionNotices = (await _corrections.ListAsync(leaseCase.Id.Value, cancellationToken))
            .Where(r => r.Purpose == "Correction")
            .Select(r => new ApplicantCorrectionNoticeDto(r.Id, r.RequiredAction, r.ClassificationCode,
                r.SectionId, r.Instructions, r.CreatedAtUtc, r.DueDateUtc, r.Status, r.Revision)).ToList();
        return new ApplicantCaseTrackingDto(
            LeaseCaseId: leaseCase.Id.Value,
            ApplicationReference: leaseCase.ApplicationReference,
            Status: leaseCase.Status.ToString(),
            CreatedAt: leaseCase.CreatedAt,
            CurrentMilestone: currentMilestone,
            CompletedMilestonesCount: completedCount,
            TotalMilestonesCount: milestones.Count,
            Milestones: milestones,
            Documents: docStatuses,
            CorrectionNotices: correctionNotices
        );
    }
}
