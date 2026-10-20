namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

public sealed record HumanEvidenceObservationRequest(string FactCode, string ValueKind, string CanonicalValue,
    int PageNumber, string Quote, string ReviewReason);
public sealed record PublishHumanEvidenceRequest(Guid DocumentId, Guid DocumentVersionId, Guid AnalysisRunId,
    Guid BaseSnapshotId, int ExpectedAnalysisRevision, IReadOnlyList<HumanEvidenceObservationRequest> Observations);
public sealed record LinkHumanEvidenceSnapshotRequest(Guid DocumentId, Guid DocumentVersionId,
    int ExpectedAnalysisRevision, int ExpectedCaseRevision);

public sealed class HumanEvidenceFactService(IDocumentAnalysisRepository analyses, IGovernedDocumentRepository documents,
    IHumanEvidencePageReader pages, IWorkflowGovernanceUnitOfWork unit, TimeProvider clock,
    LinkVerifiedFactSnapshotToCaseCommandHandler link)
{
    public async Task<object> ReadAsync(Guid caseId, Guid documentId, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(new(documentId), ct);
        if (doc?.LeaseCaseId.Value != caseId || doc.LogicalCategory != "Proposal") throw new InvalidOperationException("Proposal not found on this case.");
        var analysis = await analyses.GetByDocumentVersionIdAsync(doc.ActiveVersionId, ct);
        var run = analysis?.Runs.OrderByDescending(r => r.RunNumber).FirstOrDefault();
        var snapshot = analysis?.VerifiedFactSnapshots.OrderByDescending(s => s.PublishedAt).FirstOrDefault();
        return new { documentId, documentVersionId = doc.ActiveVersionId.Value, analysisId = analysis?.Id.Value,
            analysisRevision = analysis?.Revision, runId = run?.Id.Value, runState = run?.State.ToString(),
            latestSnapshot = snapshot == null ? null : FactVerificationMapper.ToDto(snapshot) };
    }
    public async Task<object> ReadPageAsync(Guid caseId,Guid documentId,int pageNumber,CancellationToken ct)
    {
        if(pageNumber<1)throw new ArgumentException("Select a positive source page.");
        var doc=await documents.GetByIdAsync(new(documentId),ct);
        if(doc?.LeaseCaseId.Value!=caseId || doc.LogicalCategory!="Proposal")throw new InvalidOperationException("Current Proposal required.");
        var analysis=await analyses.GetByDocumentVersionIdAsync(doc.ActiveVersionId,ct);
        var run=analysis?.Runs.OrderByDescending(x=>x.RunNumber).FirstOrDefault();
        if(run?.State!=AnalysisRunState.Completed)throw new InvalidOperationException("A completed current analysis run is required.");
        return new {documentVersionId=doc.ActiveVersionId.Value,runId=run.Id.Value,pageNumber,
            text=await pages.ReadAsync(doc.ActiveVersionId.Value,run.Id.Value,pageNumber,ct)};
    }
    public async Task LinkAsync(Guid caseId, Guid snapshotId, LinkHumanEvidenceSnapshotRequest request,
        VerifiedAuthorityContext actor, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(new(request.DocumentId), ct);
        if (doc?.LeaseCaseId.Value != caseId || doc.ActiveVersionId.Value != request.DocumentVersionId || doc.LogicalCategory != "Proposal")
            throw new InvalidOperationException("Snapshot must belong to this case's current Proposal.");
        actor.ToDomain().EnsureAuthorizes(actor.ActorId, "FactSnapshotPublisher",
            new(Domain.Authority.AuthorityScopeKind.GovernedDocument, request.DocumentId.ToString("D")), clock.GetUtcNow().UtcDateTime);
        var analysis = await analyses.GetByDocumentVersionIdAsync(new(request.DocumentVersionId), ct)
            ?? throw new InvalidOperationException("Analysis is unavailable.");
        var snapshot = analysis.VerifiedFactSnapshots.OrderByDescending(s => s.PublishedAt).FirstOrDefault();
        var run = analysis.Runs.OrderByDescending(r => r.RunNumber).FirstOrDefault();
        if (snapshot?.Id.Value != snapshotId || !snapshot.Entries.Any(e => e.HumanEvidence != null) ||
            run?.State != AnalysisRunState.Completed || run.Id != snapshot.AnalysisRunId)
            throw new InvalidOperationException("Only the latest human-evidence snapshot from the current completed run may be linked.");
        await link.HandleAsync(new(caseId, analysis.Id.Value, snapshotId,
            ExpectedLeaseCaseRevision: request.ExpectedCaseRevision, ExpectedAnalysisRevision: request.ExpectedAnalysisRevision), ct);
    }
    public async Task<VerifiedFactSnapshotDto> PublishAsync(Guid caseId, PublishHumanEvidenceRequest request,
        VerifiedAuthorityContext actor, CancellationToken ct)
    {
        var doc = await documents.GetByIdAsync(new(request.DocumentId), ct);
        if (doc?.LeaseCaseId.Value != caseId || doc.ActiveVersionId.Value != request.DocumentVersionId || doc.LogicalCategory != "Proposal")
            throw new InvalidOperationException("Human evidence requires this case's current Proposal document/version.");
        var analysis = await analyses.GetByDocumentVersionIdAsync(new(request.DocumentVersionId), ct)
            ?? throw new InvalidOperationException("Document analysis is unavailable.");
        var run = analysis.Runs.OrderByDescending(r => r.RunNumber).FirstOrDefault();
        if (run?.Id.Value != request.AnalysisRunId || run.State != AnalysisRunState.Completed || analysis.Revision != request.ExpectedAnalysisRevision)
            throw new InvalidOperationException("Analysis run/revision is stale or incomplete.");
        if (request.Observations == null || request.Observations.Count == 0) throw new ArgumentException("Human observations are required.");
        var at = clock.GetUtcNow().UtcDateTime;
        var observations = new List<HumanEvidenceFact>();
        foreach (var observation in request.Observations)
        {
            var text = await pages.ReadAsync(request.DocumentVersionId, request.AnalysisRunId, observation.PageNumber, ct);
            if (string.IsNullOrWhiteSpace(observation.Quote) || text == null || !text.Contains(observation.Quote.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("The human evidence quote must match the exact current run's cited page; unresolved transcription remains blocked.");
            if (!Enum.TryParse<AnalysisFactValueKind>(observation.ValueKind, false, out var kind) || !Enum.IsDefined(kind))
                throw new ArgumentException("Unsupported fact value kind.");
            observations.Add(new(Guid.NewGuid(), doc.Id, doc.ActiveVersionId, analysis.DocumentChecksum,
                new(observation.FactCode), new(kind, observation.CanonicalValue), observation.PageNumber,
                observation.Quote, observation.ReviewReason, actor.ActorId, at, actor.ToDomain()));
        }
        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());
        analysis.PublishHumanEvidenceSnapshot(new(request.BaseSnapshotId), snapshotId, observations,
            request.ExpectedAnalysisRevision, actor.ActorId, at, actor.ToDomain());
        await unit.CommitAsync(ct);
        return FactVerificationMapper.ToDto(analysis.VerifiedFactSnapshots.Single(s => s.Id == snapshotId));
    }
}
