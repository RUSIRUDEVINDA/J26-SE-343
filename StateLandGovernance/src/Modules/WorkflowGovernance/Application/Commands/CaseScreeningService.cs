namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System.Text.Json;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class CaseScreeningService(ILeaseCaseRepository cases, IGovernedDocumentRepository documents,
    IDocumentAnalysisRepository analyses, IDocumentCompletenessAssessmentRepository completeness,
    IDocumentRequirementProvider requirements, IProposalTemplateProvider templates,
    IConsolidatedCaseScreeningAdapter adapter,
    ICommandHandler<RequestScreeningCommand, ScreeningResultDto> requestHandler,
    ICommandHandler<RecordScreeningResultCommand, ScreeningResultDto> recordHandler,
    IScreeningInputRefresh? inputRefresh = null, ICorrectionRepository? screeningRecords = null) : ICaseScreeningReadiness
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ScreeningInputRevision> ReadInputRevisionAsync(Guid caseId, Guid snapshotId, Guid? parcelId, CancellationToken ct)
    {
        var (_, source, prepared, missing) = await LoadAsync(caseId, snapshotId, parcelId, ct);
        return new(source.Snapshot?.Id ?? Guid.Empty, prepared, missing);
    }

    public async Task<CaseScreeningState> GetAsync(Guid caseId, Guid? snapshotId, Guid? parcelId, CancellationToken ct)
    {
        StoredCaseScreening? stored = null;
        var existingCase = await cases.GetByIdAsync(new(caseId), ct) ?? throw new LeaseCaseNotFoundException(caseId);
        try { stored = JsonSerializer.Deserialize<StoredCaseScreening>(existingCase.LatestScreening?.Remarks ?? "null", JsonOptions); }
        catch (JsonException) { /* A legacy compliance-only result is not a consolidated assessment. */ }
        if (existingCase.LatestScreening?.Remarks is { } remarks)
        {
            try
            {
                using var reference = JsonDocument.Parse(remarks);
                if (reference.RootElement.TryGetProperty("schema", out var schema) && schema.GetString() == "c3-c4-reference-v1" &&
                    reference.RootElement.TryGetProperty("recordId", out var recordId) && recordId.TryGetGuid(out var id) && screeningRecords != null)
                {
                    var record = await screeningRecords.GetAsync(caseId, id, ct);
                    if (record?.Purpose == "CaseScreeningAssessment" && record.PayloadJson != null)
                        stored = JsonSerializer.Deserialize<StoredCaseScreening>(record.PayloadJson, JsonOptions);
                }
            }
            catch (JsonException) { /* Legacy remarks are not a durable consolidated reference. */ }
        }
        if (stored?.Schema != "c3-c4-v1" || stored.Assessment.ValueKind != JsonValueKind.Object) stored = null;
        // Explicit empty selection clears a persisted mock/parcel; omitted selection restores it on refresh.
        parcelId = parcelId == Guid.Empty ? null : parcelId ?? stored?.ParcelId;
        var (leaseCase, source, prepared, missing) = await LoadAsync(caseId, snapshotId, parcelId, ct);
        var current = IsStoredCurrent(stored, source, prepared, missing);
        return new(caseId, leaseCase.Revision, source.Snapshot?.Id, parcelId,
            missing.Count > 0 ? "BlockedIncomplete" : stored is null
                ? leaseCase.LatestScreening?.Outcome == StateLandGovernance.WorkflowGovernance.Domain.Screening.ScreeningOutcome.Pending ? "Pending" : "ReadyForScreening"
                : current ? "Assessed" : "Stale",
            current, missing, stored, prepared.IsDevelopmentSpatialMock, prepared.SpatialProvenanceLabel);
    }

    public async Task<CaseScreeningState> RunAsync(Guid caseId, StartCaseScreeningRequest request, Guid requestingOfficerId, CancellationToken ct)
    {
        if (requestingOfficerId == Guid.Empty) throw new UnauthorizedAccessException("An authenticated officer identity is required.");
        if (screeningRecords == null) throw new InvalidOperationException("Durable C3 screening result storage is unavailable.");
        var (leaseCase, source, prepared, missing) = await LoadAsync(caseId, request.VerifiedFactSnapshotId, request.ParcelId, ct);
        if (leaseCase.Revision != request.ExpectedCaseRevision)
            throw new LeaseCaseConcurrencyException(caseId, request.ExpectedCaseRevision, leaseCase.Revision);
        if (missing.Count > 0)
            return new(caseId, leaseCase.Revision, source.Snapshot?.Id, request.ParcelId, "BlockedIncomplete", false, missing, null,
                prepared.IsDevelopmentSpatialMock, prepared.SpatialProvenanceLabel);
        await adapter.EnsureCanEvaluateAsync(prepared, ct);
        var pending = await requestHandler.HandleAsync(new(caseId, leaseCase.Revision, Remarks:
            JsonSerializer.Serialize(new { schema = "c3-c4-pending-v1", requestingOfficerId,
                source.CaseId, snapshotId = source.Snapshot!.Id, prepared.InputFingerprint, prepared.PolicyFingerprint }, JsonOptions)), ct);
        // C4 performs one consolidated orchestration. There are no separate frontend sub-engine calls.
        var result = await adapter.EvaluateAsync(prepared, ct);
        ValidateResultBinding(result, source, prepared);
        var fresh = inputRefresh == null
            ? await ReadInputRevisionAsync(caseId, request.VerifiedFactSnapshotId, request.ParcelId, ct)
            : await inputRefresh.ReadAsync(caseId, request.VerifiedFactSnapshotId, request.ParcelId, ct);
        if (fresh.MissingInputs.Count > 0 || fresh.SnapshotId != source.Snapshot?.Id
            || fresh.Prepared.InputFingerprint != prepared.InputFingerprint || fresh.Prepared.PolicyFingerprint != prepared.PolicyFingerprint)
            throw new InvalidOperationException("Screening inputs changed during evaluation. The result was not accepted as current.");
        var stored = new StoredCaseScreening("c3-c4-v1", caseId, source.Snapshot!.Id, request.ParcelId,
            prepared.InputFingerprint, prepared.PolicyFingerprint, result, prepared.InputJson, prepared.BindingJson, requestingOfficerId);
        var outcome = result.GetProperty("overallStatus").GetString() switch
        {
            "Cleared" => "Cleared",
            "RequiresReview" or "RecommendedWithConditions" => "Advisory",
            _ => "Blocked"
        };
        if (result.TryGetProperty("consolidatedFindings", out var findings) && findings.EnumerateArray().Any(f =>
            f.TryGetProperty("isBlocking", out var blocking) && blocking.ValueKind == JsonValueKind.True)) outcome = "Blocked";
        var assessmentId = result.GetProperty("assessmentId").GetGuid();
        var recordId = Guid.NewGuid(); var at = DateTime.UtcNow;
        await screeningRecords.SaveAsync(new CorrectionRecord { Id = recordId, LeaseCaseId = caseId, Purpose = "CaseScreeningAssessment",
            RequestedBy = requestingOfficerId, CreatedAtUtc = at, DueDateUtc = at, Status = outcome,
            PayloadJson = JsonSerializer.Serialize(stored, JsonOptions) }, ct);
        // The record and compact aggregate reference commit together through the existing shared C3 unit of work.
        // Domain remarks remain bounded; complete findings/evidence/provenance are never truncated.
        await recordHandler.HandleAsync(new(caseId, pending.ScreeningResultId, source.Snapshot.Id, outcome,
            leaseCase.Revision, assessmentId, JsonSerializer.Serialize(new { schema = "c3-c4-reference-v1", recordId, assessmentId }, JsonOptions)), ct);
        return await GetAsync(caseId, source.Snapshot.Id, request.ParcelId, ct);
    }

    public static void ValidateResultBinding(JsonElement result, CaseScreeningSource source, PreparedCaseScreening prepared)
    {
        using var input = JsonDocument.Parse(prepared.InputJson ?? throw new InvalidOperationException("Screening input is incomplete."));
        try
        {
        if (result.GetProperty("assessmentId").GetGuid() == Guid.Empty
            || !Guid.TryParse(result.GetProperty("caseId").GetString(), out var caseId) || caseId != source.CaseId
            || result.GetProperty("verifiedFactSnapshotId").GetGuid() != source.Snapshot?.Id
            || result.GetProperty("proposalVersion").GetString() != input.RootElement.GetProperty("proposalVersion").GetString())
            throw new InvalidOperationException("C4 result does not match the requested case, verified snapshot and input version.");
        }
        catch (Exception ex) when (ex is KeyNotFoundException or FormatException)
        {
            throw new InvalidOperationException("C4 returned an incomplete or malformed case/snapshot binding.", ex);
        }
    }

    public static bool IsStoredCurrent(StoredCaseScreening? stored, CaseScreeningSource source,
        PreparedCaseScreening prepared, IReadOnlyList<string> missing)
    {
        if (stored is null || stored.Schema != "c3-c4-v1" || missing.Count != 0
            || stored.CaseId != source.CaseId || stored.SnapshotId != source.Snapshot?.Id
            || stored.ParcelId != source.ParcelId || stored.InputFingerprint != prepared.InputFingerprint
            || stored.PolicyFingerprint != prepared.PolicyFingerprint || stored.InputJson is null || stored.SourceBindingsJson is null
            || stored.InputJson != prepared.InputJson || stored.SourceBindingsJson != prepared.BindingJson) return false;
        try { ValidateResultBinding(stored.Assessment, source, prepared); return true; }
        catch (InvalidOperationException) { return false; }
    }

    public static bool IsSnapshotCurrentFor(Guid caseId, VerifiedFactSnapshotDto snapshot,
        GovernedDocumentDto? document, DocumentAnalysisDto? analysis)
    {
        var version = document?.Versions.SingleOrDefault(v => v.Id == document.ActiveVersionId);
        return document?.LeaseCaseId == caseId && document.Id == snapshot.GovernedDocumentId
            && document.ActiveVersionId == snapshot.DocumentVersionId
            && version?.ChecksumValue == snapshot.ChecksumValue && version.ChecksumAlgorithm == snapshot.ChecksumAlgorithm
            && analysis?.Id == snapshot.DocumentAnalysisId && analysis.GovernedDocumentId == document.Id
            && analysis.DocumentVersionId == snapshot.DocumentVersionId
            && analysis.ChecksumValue == snapshot.ChecksumValue && analysis.ChecksumAlgorithm == snapshot.ChecksumAlgorithm
            && analysis.ActiveRun?.Id == snapshot.AnalysisRunId && analysis.ActiveRun.State == "Completed"
            && analysis.ActiveRun.Result?.Id == snapshot.AnalysisRunResultId;
    }

    private async Task<(LeaseCase Case, CaseScreeningSource Source, PreparedCaseScreening Prepared, List<string> Missing)>
        LoadAsync(Guid caseId, Guid? expectedSnapshot, Guid? parcelId, CancellationToken ct)
    {
        var leaseCase = await cases.GetByIdAsync(new(caseId), ct) ?? throw new LeaseCaseNotFoundException(caseId);
        if (expectedSnapshot.HasValue && expectedSnapshot != leaseCase.CurrentVerifiedFactSnapshotId)
            throw new InvalidOperationException("The requested verified snapshot is not current for this case.");
        var missing = new List<string>();
        var docs = (await documents.GetByLeaseCaseIdAsync(new(caseId), ct)).Where(d => CaseInputDocuments.IsIntakeInput(d.LogicalCategory)).ToList();
        var docDtos = docs.Select(GovernedDocumentMapper.ToDto).OrderBy(d => d.Id).ToList();
        VerifiedFactSnapshotDto? snapshot = null;
        IReadOnlyList<CandidateFactDto>? factEvidence = null;
        if (leaseCase.CurrentVerifiedFactSnapshotId == Guid.Empty) missing.Add("A published, linked human-verified fact snapshot is required.");
        else
        {
            var domainSnapshot = await analyses.GetSnapshotByIdAsync(new(leaseCase.CurrentVerifiedFactSnapshotId), ct);
            if (domainSnapshot is null) missing.Add("The linked verified snapshot cannot be retrieved.");
            else
            {
                snapshot = FactVerificationMapper.ToDto(domainSnapshot);
                var document = docs.SingleOrDefault(d => d.Id == domainSnapshot.GovernedDocumentId);
                var analysis = await analyses.GetByIdAsync(domainSnapshot.DocumentAnalysisId, ct);
                var analysisDto = analysis is null ? null : DocumentAnalysisMapper.ToDto(analysis);
                factEvidence = analysisDto?.ActiveRun?.Result?.ExtractedCandidateFacts
                    .Where(f => snapshot.Entries.Any(e => e.SourceExtractedFactId == f.Id)).ToList();
                if (!IsSnapshotCurrentFor(caseId, snapshot, document is null ? null : GovernedDocumentMapper.ToDto(document), analysisDto))
                    missing.Add("The verified snapshot must belong to this case and its active document version and analysis run.");
                if (snapshot.SnapshotOutcome != "VerifiedFacts" || snapshot.PublishedFactCount == 0)
                    missing.Add("The snapshot contains no publishable verified facts for screening.");
            }
        }
        var latestDocs = (await completeness.GetByLeaseCaseIdAsync(new(caseId), ct)).OrderByDescending(x => x.AssessedAt).FirstOrDefault();
        if (latestDocs is null)
            missing.Add("Supporting-document completeness has not been assessed. Run Check A using the applicable configured requirement set.");
        else if (await requirements.GetRequirementSetAsync(latestDocs.RequirementSetIdentifier, latestDocs.RequirementSetVersion, ct) is null)
            missing.Add("Requirements not configured: the supporting-document assessment requirement set is unavailable.");
        if (latestDocs is null || latestDocs.Outcome != CompletenessAssessmentOutcome.Complete
            || !latestDocs.AssessedDocuments.Select(x => x.DocumentVersionId.Value).ToHashSet().SetEquals(docs.Select(x => x.ActiveVersionId.Value)))
            missing.Add("A current, complete supporting-document assessment is required.");
        var proposal = leaseCase.GetLatestRecordedProposalContentAssessment();
        var currentTemplate = proposal is null ? null : await templates.GetTemplateAsync(proposal.SourceBinding.TemplateId, proposal.SourceBinding.TemplateVersion, ct);
        if (proposal is null)
            missing.Add("Proposal-content completeness has not been assessed. Run Check B using the applicable configured template, then obtain officer confirmation.");
        else if (currentTemplate is null)
            missing.Add("Requirements not configured: the proposal assessment template is unavailable.");
        if (proposal is null || !proposal.CanSatisfyCompletenessGate
            || currentTemplate?.DefinitionDigest != proposal.TemplateSnapshot.DefinitionDigest
            || !docs.Any(d => d.Id == proposal.SourceBinding.ProposalDocumentId && d.ActiveVersionId == proposal.SourceBinding.DocumentVersionId))
            missing.Add("A current, explicitly confirmed complete proposal-content assessment is required.");
        var completenessBinding = JsonSerializer.Serialize(new { documentAssessment = latestDocs?.Id.Value,
            documentRevision = latestDocs?.Revision, requirementSet = latestDocs?.RequirementSetIdentifier,
            requirementVersion = latestDocs?.RequirementSetVersion, proposalAssessment = proposal?.Id.Value,
            proposalTemplate = proposal?.TemplateSnapshot.TemplateId.Value, proposalTemplateVersion = proposal?.TemplateSnapshot.TemplateVersion,
            proposalDefinitionDigest = proposal?.TemplateSnapshot.DefinitionDigest }, JsonOptions);
        var source = new CaseScreeningSource(caseId, leaseCase.ApplicationReference, snapshot, docDtos, parcelId, completenessBinding, factEvidence);
        var prepared = await adapter.PrepareAsync(source, ct);
        missing.AddRange(prepared.MissingInputs);
        return (leaseCase, source, prepared, missing.Distinct().ToList());
    }
}
