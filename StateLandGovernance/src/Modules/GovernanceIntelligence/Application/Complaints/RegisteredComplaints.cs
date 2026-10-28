using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
namespace StateLandGovernance.GovernanceIntelligence.Application.Complaints;

public sealed record ComplaintReference(Guid? CaseId, string? ParcelId);
public sealed record SubmitRegisteredComplaint(Guid SubmissionKey, string Subject, string ComplaintText, string? CaseReference, string? ParcelId, string? LandAddress = null);
public sealed record ComplaintAttempt(Guid Id, DateTimeOffset StartedAtUtc, DateTimeOffset? FinishedAtUtc, string State, Guid? AssessmentId, string? FailureCode);
public sealed record ComplaintReview(Guid Id, Guid ActorId, DateTimeOffset AtUtc, string Status, string Reason);
public sealed record RegisteredComplaint(Guid Id, Guid ApplicantId, Guid SubmissionKey, string InputHash, string Subject,
    string ComplaintText, Guid? CaseId, string? ParcelId, DateTimeOffset SubmittedAtUtc, int Revision,
    string ClassificationState, string ReviewStatus, ComplaintAttempt[] Attempts, ComplaintReview[] Reviews,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? LandAddress = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? UnverifiedCaseReference = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? UnverifiedParcelReference = null);
public sealed record RegisteredComplaintDetail(RegisteredComplaint Complaint, IReadOnlyList<ComplaintClassificationAssessment> Classifications, ComplaintHandlingView? Handling = null);
// Host adapter validates references without equating complaint eligibility with land ownership.
public interface IComplaintReferenceAccess
{
    Task<ComplaintReference> ValidateAsync(string? caseReference, string? parcelId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor, CancellationToken ct);
}
// Standalone module composition fails closed until the host supplies trusted peer reads.
public sealed class UnavailableComplaintReferenceAccess:IComplaintReferenceAccess
{
    public Task<ComplaintReference> ValidateAsync(string? reference,string? parcel,CancellationToken ct)
        =>throw new InvalidOperationException("Complaint reference/assignment integration is unavailable.");
    public Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor,CancellationToken ct)
        =>Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}
public interface IRegisteredComplaintStore
{
    Task<RegisteredComplaint?> GetAsync(Guid id, CancellationToken ct);
    Task<RegisteredComplaint?> FindSubmissionAsync(Guid actor, Guid key, CancellationToken ct);
    Task<IReadOnlyList<RegisteredComplaint>> ListAsync(Guid? applicant, IReadOnlyList<Guid> reviewerCases, CancellationToken ct);
    Task<bool> InsertAsync(RegisteredComplaint complaint, CancellationToken ct);
    Task SaveAsync(RegisteredComplaint complaint, int expectedRevision, ComplaintClassificationAssessment? assessment, CancellationToken ct);
    Task<Guid?> ComplaintForAssessmentAsync(Guid assessmentId, CancellationToken ct);
}
public sealed class ComplaintConcurrencyException : Exception { }
public sealed class RegisteredComplaintService(IRegisteredComplaintStore store, IComplaintReferenceAccess access,
    IComplaintClassificationClient classifier, IComplaintClassificationAssessmentStore classifications, TimeProvider time,
    IComplaintRoutingStore? routing = null, ComplaintRoutingPolicy? routingPolicy = null, IComplaintHandlingStore? handling = null)
{
    public async Task<RegisteredComplaint> SubmitAsync(Guid actor, SubmitRegisteredComplaint input, CancellationToken ct)
    {
        if(actor==Guid.Empty)throw new UnauthorizedAccessException();
        if(input.SubmissionKey==Guid.Empty || string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Length>200 ||
           string.IsNullOrWhiteSpace(input.ComplaintText) || input.ComplaintText.EnumerateRunes().Count()>ComplaintClassificationConstraints.MaximumComplaintTextLength ||
           input.CaseReference?.Length>100 || input.ParcelId?.Length>100 || input.LandAddress?.Length>2000 ||
           input.LandAddress?.Any(c=>char.IsControl(c)&&c is not ('\r' or '\n' or '\t'))==true)
            throw new ArgumentException("Supply a subject, English complaint text (up to 10000 characters), a land address of up to 2000 characters and optional references of up to 100 characters.");
        // Keep legacy hashes byte-for-byte compatible when the optional address is absent.
        var canonical=input.LandAddress==null?JsonSerializer.Serialize(new{input.Subject,input.ComplaintText,input.CaseReference,input.ParcelId}):JsonSerializer.Serialize(new{input.Subject,input.ComplaintText,input.CaseReference,input.ParcelId,input.LandAddress});
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var old=await store.FindSubmissionAsync(actor,input.SubmissionKey,ct);
        if(old!=null){if(old.InputHash!=fingerprint)throw new ComplaintConcurrencyException();return old;}
        // Standalone intake: supplied location/references are unverified assertions, never trusted peer links.
        // No C1/C3 call is needed, including when a supplied reference is unknown or a peer is unavailable.
        var complaint=new RegisteredComplaint(Guid.NewGuid(),actor,input.SubmissionKey,fingerprint,input.Subject,input.ComplaintText,null,null,time.GetUtcNow(),0,"Pending","Open",[],[],
            input.LandAddress,input.CaseReference,input.ParcelId);
        if(!await store.InsertAsync(complaint,ct)){
            old=await store.FindSubmissionAsync(actor,input.SubmissionKey,ct);
            if(old==null||old.InputHash!=fingerprint)throw new ComplaintConcurrencyException();return old;
        }
        return await ClassifyAsync(actor,false,complaint.Id,0,ct);
    }
    public async Task<RegisteredComplaint> RequireAsync(Guid actor,bool officer,Guid id,CancellationToken ct)
    {
        var row=await store.GetAsync(id,ct)??throw new KeyNotFoundException();
        if(actor==Guid.Empty)throw new KeyNotFoundException();
        if(officer){
            if(routing==null||routingPolicy==null||!await routingPolicy.EligibleAsync(actor,ct))throw new KeyNotFoundException();
            if(handling!=null){if(!await handling.IsAssignedAsync(actor,id,ct))throw new KeyNotFoundException();return row;}
            var assignment=await routing.CurrentAsync(id,ct);
            if(assignment?.TargetActorId!=actor || !(await routingPolicy.SnapshotAsync(ct)).Officers.Any(x=>x.ActorId==actor&&x.OfficeCode==assignment.TargetOfficeCode))throw new KeyNotFoundException();
        }else if(row.ApplicantId!=actor)throw new KeyNotFoundException();
        return row;
    }
    public async Task<IReadOnlyList<RegisteredComplaint>> ListAsync(Guid actor,bool officer,CancellationToken ct)
    {
        if(actor==Guid.Empty)throw new UnauthorizedAccessException();
        if(!officer)return await store.ListAsync(actor,[],ct);
        if(routing==null||routingPolicy==null||!await routingPolicy.EligibleAsync(actor,ct))return [];
        var rows=await routing.AssignedAsync(actor,ct);
        var allowed=new List<RegisteredComplaint>();
        foreach(var row in rows){try{await RequireAsync(actor,true,row.Id,ct);allowed.Add(row);}catch(KeyNotFoundException){}}
        return allowed;
    }
    public async Task<RegisteredComplaintDetail> DetailAsync(Guid actor,bool officer,Guid id,CancellationToken ct)
    {
        var row=await RequireAsync(actor,officer,id,ct);var results=new List<ComplaintClassificationAssessment>();
        foreach(var attempt in row.Attempts.Where(a=>a.AssessmentId!=null)){
            var saved=await classifications.GetByIdAsync(attempt.AssessmentId!.Value,ct);
            if(saved!=null)results.Add(saved);
        }
        await RequireAsync(actor,officer,id,ct); // Recheck live assignment after asynchronous reads.
        return new(row,results,handling==null?null:await handling.ViewAsync(id,false,officer,ct));
    }
    public async Task<RegisteredComplaint> ClassifyAsync(Guid actor,bool officer,Guid id,int revision,CancellationToken ct)
    {
        var row=await RequireAsync(actor,officer,id,ct);var now=time.GetUtcNow();
        if(row.Revision!=revision || row.ClassificationState=="Classified" || (row.ClassificationState=="InProgress"&&row.Attempts.Last().StartedAtUtc.AddMinutes(5)>now))throw new ComplaintConcurrencyException();
        var attempts=row.Attempts.Select(a=>a.State=="InProgress"?a with{State="Interrupted",FinishedAtUtc=now,FailureCode="Interrupted"}:a).Append(new ComplaintAttempt(Guid.NewGuid(),now,null,"InProgress",null,null)).ToArray();
        var claim=row with{Revision=revision+1,ClassificationState="InProgress",Attempts=attempts};
        await store.SaveAsync(claim,revision,null,ct); // Durable registration and CAS claim before any network call.
        ComplaintClassificationResult response;
        var binding="registered-complaint:"+id.ToString("D");
        try{response=await classifier.ClassifyAsync(row.ComplaintText,binding,ct);}
        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;} // Lease can be recovered by controlled retry.
        catch{response=ComplaintClassificationResult.Failure(ComplaintClassificationErrorCode.UpstreamFailure,"Classifier unavailable.");}
        ComplaintClassificationAssessment? assessment=null;
        if(response.IsSuccess && response.Prediction!.CaseId==binding){
            var p=response.Prediction;assessment=new(Guid.NewGuid(),binding,row.ComplaintText,p.ModelVersion,p.PredictedCategory,p.ClassProbabilities,p.AdvisoryNote,p.ClosedSetNote,time.GetUtcNow());
        }
        var state=assessment==null?"Failed":"Classified";
        attempts[^1]=attempts[^1] with{FinishedAtUtc=time.GetUtcNow(),State=state,AssessmentId=assessment?.AssessmentId,FailureCode=assessment==null?(response.Error?.Code.ToString()??"InvalidResponse"):null};
        var completed=claim with{Revision=claim.Revision+1,ClassificationState=state,Attempts=attempts};
        await store.SaveAsync(completed,claim.Revision,assessment,ct); // Assessment + link + state in one repository transaction.
        await RequireAsync(actor,officer,id,ct);
        return completed;
    }
    public async Task<RegisteredComplaint> ReviewAsync(Guid actor,Guid id,int revision,string status,string reason,CancellationToken ct,int? expectedHandlingRevision=null)
    {
        await RequireAsync(actor,true,id,ct);
        if(!new[]{"UnderReview","InformationRequested","ReviewRecorded"}.Contains(status)||string.IsNullOrWhiteSpace(reason)||reason.Length>4000)throw new ArgumentException("Choose a triage status and give a reason (up to 4000 characters). No verification or resolution is asserted.");
        if(routing==null)throw new KeyNotFoundException();
        var updated=await routing.ReviewAsync(actor,id,revision,status,reason,ct,expectedHandlingRevision);
        await RequireAsync(actor,true,id,ct);
        return updated;
    }
}
