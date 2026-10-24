using System.Security.Claims;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
namespace StateLandGovernance.Api.Security;

/// <summary>Explicit synthetic designation bound to the uploaded document version, never filename authorization.</summary>
public sealed class UploadedCaseResearchScenarios(DevelopmentAccounts accounts, IHostEnvironment env,
    IHttpContextAccessor http, ICaseAssignmentStore assignments, ICorrectionRepository records,
    IGovernedDocumentRepository documents, IWorkflowGovernanceUnitOfWork unit)
{
    public sealed record Designation(Guid DocumentId, Guid VersionId, string Reason);
    public bool Local => env.IsDevelopment() && http.HttpContext is { } context && accounts.Local(context);
    public async Task<bool> AssignedAsync(Guid caseId, Guid? documentId = null, CancellationToken ct = default)
    {
        var user = http.HttpContext?.User;
        if (!Local || user?.Identity?.IsAuthenticated != true || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor)) return false;
        var state = await assignments.ReadAsync(caseId, ct);
        return state?.Active is { } grant && grant.ActorId == actor && CaseAssignmentService.IsActive(grant, DateTimeOffset.UtcNow) &&
            (documentId == null || grant.DocumentId == documentId) && grant.Capabilities.Contains("CaseWorkspaceReviewer") && grant.Capabilities.Contains("FactVerifier");
    }
    public async Task<Designation?> ReadAsync(Guid caseId, CancellationToken ct = default)
    {
        if (!await AssignedAsync(caseId, ct:ct)) return null;
        var record = (await records.ListAsync(caseId, ct)).Where(x => x.Purpose == "DevelopmentSyntheticDesignation")
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        if (record?.PayloadJson == null) return null;
        var value = JsonSerializer.Deserialize<Designation>(record.PayloadJson);
        if (value == null || !await AssignedAsync(caseId,value.DocumentId,ct)) return null;
        var document = await documents.GetByIdAsync(new(value.DocumentId),ct);
        return document?.LeaseCaseId.Value == caseId && document.ActiveVersionId.Value == value.VersionId ? value : null;
    }
    public async Task<Designation> DesignateAsync(Guid caseId, Guid documentId, Guid versionId, string reason, CancellationToken ct)
    {
        if (!await AssignedAsync(caseId,documentId,ct)) throw new UnauthorizedAccessException("An active durable case/document review assignment is required.");
        var document = await documents.GetByIdAsync(new(documentId),ct);
        if(document?.LeaseCaseId.Value != caseId || document.ActiveVersionId.Value != versionId || document.LogicalCategory != "Proposal")
            throw new InvalidOperationException("Select this case's exact current Proposal version.");
        if(string.IsNullOrWhiteSpace(reason) || reason.Length > 2000) throw new ArgumentException("Record why the reviewed source is explicitly synthetic.");
        var value = new Designation(documentId,versionId,reason.Trim()); var now=DateTime.UtcNow;
        await records.SaveAsync(new CorrectionRecord { Id=Guid.NewGuid(),LeaseCaseId=caseId,RequestedBy=Guid.Parse(http.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.HttpContext.User.FindFirstValue("sub")!),
            CreatedAtUtc=now,DueDateUtc=now,Purpose="DevelopmentSyntheticDesignation",Status="Recorded",Revision=1,
            RequiredAction="DesignateSyntheticResearchSource",Instructions=reason.Trim(),PayloadJson=JsonSerializer.Serialize(value) },ct);
        await unit.CommitAsync(ct);return value;
    }
    public async Task<bool> CanUseAsync(Guid caseId,CancellationToken ct=default) => await ReadAsync(caseId,ct)!=null;
    public static bool SupportedParcel(Guid? parcel) => parcel == PortalResearchEvidence.ManualOverlapCoverage ||
        parcel == PortalResearchEvidence.ManualNoConflictCoverage || parcel == Guid.Parse("de4c0000-0000-4000-8000-000000000031") || parcel == Guid.Parse("de4c0000-0000-4000-8000-000000000033");
}
public sealed class UploadedCaseDevelopmentSpatial(DevelopmentSpatialInputProvider existing, UploadedCaseResearchScenarios scenarios,
    IHttpContextAccessor http) : IDevelopmentSpatialInputProvider
{
    public async Task<DevelopmentSpatialInput?> ReadAsync(Guid caseId,Guid? parcelId,CancellationToken ct)
    {
        if(UploadedCaseResearchScenarios.SupportedParcel(parcelId) && await scenarios.CanUseAsync(caseId,ct))
        {
            var fixture=await existing.ReadAsync(PortalResearchEvidence.ManualProposalCase,parcelId,ct);
            if(fixture!=null){http.HttpContext!.Items["UploadedSyntheticScreeningCase"]=caseId;return fixture with { CaseId=caseId,
                Label=fixture.Label+"; explicitly designated uploaded synthetic Proposal; actual OCR/reviews unchanged" };}
        }
        return await existing.ReadAsync(caseId,parcelId,ct);
    }
}
