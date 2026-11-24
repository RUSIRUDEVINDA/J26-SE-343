using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public sealed class ComplaintHandlingStore(GovernanceIntelligenceDbContext db, IComplaintActorContext actor,
    ComplaintRoutingPolicy policy, IRegisteredComplaintStore complaints) : IComplaintHandlingStore
{
    private DbSet<ComplaintHandlingEntity> Events => db.Set<ComplaintHandlingEntity>();
    public async Task<(IReadOnlyList<RegisteredComplaint> Items,string? Next)> QueueAsync(string filter,string? cursor,CancellationToken ct)
    {
        await policy.RequireCentralAsync(actor.ActorId,false,ct);
        if(filter is not ("unassigned" or "commissioner" or "ds" or "terminal" or "not-ds"))throw new ArgumentException("Invalid handling queue.");
        Guid? after=null;if(cursor!=null){if(!Guid.TryParseExact(cursor,"D",out var id))throw new ArgumentException("Invalid cursor.");after=id;}
        var latest=Events.Where(e=>!Events.Any(n=>n.ComplaintId==e.ComplaintId&&n.Revision>e.Revision));
        var assignments=db.Set<ComplaintAssignmentEntity>();
        var rows=db.Set<RegisteredComplaintEntity>().AsNoTracking().Where(c=>
            filter=="commissioner"?latest.Any(e=>e.ComplaintId==c.Id&&e.HandlingMode=="Commissioner"):
            filter=="terminal"?latest.Any(e=>e.ComplaintId==c.Id&&e.HandlingMode=="None"):
            filter=="ds"?(latest.Any(e=>e.ComplaintId==c.Id&&e.HandlingMode=="Ds")||(!Events.Any(e=>e.ComplaintId==c.Id)&&assignments.Any(a=>a.ComplaintId==c.Id))):
            filter=="not-ds"?!(latest.Any(e=>e.ComplaintId==c.Id&&e.HandlingMode=="Ds")||(!Events.Any(e=>e.ComplaintId==c.Id)&&assignments.Any(a=>a.ComplaintId==c.Id))):
            !Events.Any(e=>e.ComplaintId==c.Id)&&!assignments.Any(a=>a.ComplaintId==c.Id));
        var anchor=after==null?null:await db.Set<RegisteredComplaintEntity>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==after.Value,ct);
        if(after!=null&&anchor==null)throw new ArgumentException("Queue cursor expired. Refresh the queue.");
        var page=await ComplaintQueueOrder.Apply(rows,anchor).Take(51).ToArrayAsync(ct);
        await policy.RequireCentralAsync(actor.ActorId,false,ct);
        return(page.Take(50).Select(x=>JsonSerializer.Deserialize<RegisteredComplaint>(x.PayloadJson)!).ToArray(),page.Length>50?page[49].Id.ToString("D"):null);
    }
    private async Task<ComplaintHandlingEntity?> LatestAsync(Guid id,CancellationToken ct) => await Events.AsNoTracking().Where(x=>x.ComplaintId==id).OrderByDescending(x=>x.Revision).FirstOrDefaultAsync(ct);
    private async Task<ComplaintAssignmentEntity?> AssignmentAsync(Guid id,CancellationToken ct) => await db.Set<ComplaintAssignmentEntity>().AsNoTracking().Where(x=>x.ComplaintId==id).OrderByDescending(x=>x.Revision).FirstOrDefaultAsync(ct);
    private static HandlingState State(ComplaintHandlingEntity e) => new(e.Revision,e.ToState,e.HandlingMode,e.ResponsibleActorId,e.AssignmentEventId,"HandlingEvent");
    public async Task<HandlingState> CurrentAsync(Guid id,CancellationToken ct)
    {
        var e=await LatestAsync(id,ct);if(e!=null)return State(e);
        var assignment=await AssignmentAsync(id,ct);
        return assignment==null?new(0,"Unassigned","None",null,null,"Initial"):new(0,"AssignedToDs","Ds",assignment.TargetActorId,assignment.Id,"LegacyAssignment");
    }
    public async Task<bool> IsAssignedAsync(Guid target,Guid id,CancellationToken ct)
    {
        if(!await policy.EligibleAsync(target,ct))return false;
        var state=await CurrentAsync(id,ct);var assignment=await AssignmentAsync(id,ct);
        return state.Mode=="Ds"&&!ComplaintHandlingRules.Terminal(state.Status)&&state.ResponsibleActorId==target&&assignment!=null&&
            assignment.Id==state.AssignmentEventId&&assignment.TargetActorId==target&&
            (await policy.SnapshotAsync(ct)).Officers.Any(x=>x.ActorId==target&&x.OfficeCode==assignment.TargetOfficeCode);
    }
    private async Task RequireScopeAsync(Guid id,bool central,bool officer,CancellationToken ct)
    {
        if(central)await policy.RequireCentralAsync(actor.ActorId,false,ct);
        var complaint=await complaints.GetAsync(id,ct)??throw new KeyNotFoundException();
        if(!central&&(officer?!await IsAssignedAsync(actor.ActorId,id,ct):actor.ActorId==Guid.Empty||complaint.ApplicantId!=actor.ActorId))throw new KeyNotFoundException();
    }
    private async Task<string[]> AllowedAsync(HandlingState state,bool central,bool officer,CancellationToken ct)
    {
        var allowed=new List<string>();
        if(!policy.WritesEnabled)return [];
        if(central)
        {
            var grant=await policy.RequireCentralAsync(actor.ActorId,false,ct);
            if(!policy.WritesEnabled)return [];
            foreach(var operation in grant.ReviewOperations)
            {
                if(operation=="AssignDs"||!grant.Permissions.Contains(ComplaintRoutingPolicy.Review))continue;
                if(operation is not ("TakeDirect" or "Resolve" or "Close" or "Reopen") && (state.Mode!="Commissioner"||state.ResponsibleActorId!=actor.ActorId))continue;
                if(operation=="Resolve"&&state.Mode=="Commissioner"&&state.ResponsibleActorId!=actor.ActorId)continue;
                if(operation=="TakeDirect"&&state.Mode=="Commissioner"&&state.ResponsibleActorId==actor.ActorId)continue;
                try{ComplaintHandlingRules.Next(state.Status,operation);allowed.Add(operation);}catch(ComplaintConcurrencyException){}
            }
            if(!ComplaintHandlingRules.Terminal(state.Status)&&grant.Permissions.Contains(ComplaintRoutingPolicy.Assign)&&
               (state.Mode!="Commissioner"||(state.ResponsibleActorId==actor.ActorId&&grant.Permissions.Contains(ComplaintRoutingPolicy.Review)&&grant.ReviewOperations.Contains("AssignDs"))))allowed.Add("AssignDs");
        }
        else if(officer&&state.Mode=="Ds")
            foreach(var op in ComplaintHandlingRules.DsOperations){try{ComplaintHandlingRules.Next(state.Status,op);allowed.Add(op);}catch(ComplaintConcurrencyException){}}
        return allowed.ToArray();
    }
    public async Task<ComplaintHandlingView> ViewAsync(Guid id,bool central,bool officer,CancellationToken ct)
    {
        await RequireScopeAsync(id,central,officer,ct);
        var state=await CurrentAsync(id,ct);
        var rows=await Events.AsNoTracking().Where(x=>x.ComplaintId==id).OrderBy(x=>x.Revision).ToArrayAsync(ct);
        var history=new List<HandlingHistoryItem>();
        foreach(var e in rows)
        {
            var internalAccess=central||(officer&&e.HandlingMode=="Ds"&&e.AssignmentEventId==state.AssignmentEventId&&e.ActorId==actor.ActorId);
            var routingReasonAccess=officer&&state.Mode=="Ds"&&state.ResponsibleActorId==actor.ActorId&&
                e.AssignmentEventId==state.AssignmentEventId&&e.Operation is "AssignDs" or "ReassignDs";
            if(!internalAccess&&string.IsNullOrWhiteSpace(e.PublicSummary))continue;
            history.Add(new(e.Id,e.Revision,e.Operation,e.ToState,e.ActedAtUtc,e.PublicSummary,
                internalAccess?e.ActorId:null,internalAccess||routingReasonAccess?e.Reason:null,
                internalAccess?JsonSerializer.Deserialize<Dictionary<string,string>>(e.DetailsJson):null,
                internalAccess?JsonSerializer.Deserialize<ComplaintEvidenceReference[]>(e.EvidenceReferencesJson):null));
        }
        var allowed=await AllowedAsync(state,central,officer,ct);
        var current=await CurrentAsync(id,ct);
        await RequireScopeAsync(id,central,officer,ct);
        if(!central&&!officer)current=current with{ResponsibleActorId=null,AssignmentEventId=null};
        return new(current,allowed,history);
    }
    private Task LockAsync(Guid id,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"c4-complaint:"+id.ToString("D")},0))",ct);
    private async Task ValidateEvidenceAsync(Guid id,ComplaintEvidenceReference[] references,CancellationToken ct)
    {
        foreach(var reference in references.Where(x=>x.Kind=="C4Record"))
        {
            if(!Guid.TryParse(reference.Reference,out var record))throw new ArgumentException("A C4 evidence reference must identify a saved complaint-related record.");
            var related=await Events.AnyAsync(x=>x.Id==record&&x.ComplaintId==id,ct)||
                await db.Set<ComplaintAssignmentEntity>().AnyAsync(x=>x.Id==record&&x.ComplaintId==id,ct)||
                await db.Set<RegisteredComplaintAssessmentLinkEntity>().AnyAsync(x=>x.AssessmentId==record&&x.ComplaintId==id,ct);
            if(!related)related=(await complaints.GetAsync(id,ct))!.Reviews.Any(x=>x.Id==record);
            if(!related)throw new ArgumentException("Evidence reference is not an accessible record for this complaint.");
        }
    }
    private async Task<ComplaintHandlingEntity> AppendAsync(Guid id,HandlingState previous,string operation,string next,string mode,Guid? responsible,Guid? assignment,
        Guid key,string hash,string reason,Dictionary<string,string> details,ComplaintEvidenceReference[] references,string summary,CentralComplaintGrant? grant,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("Handling writes require the scoped complaint transaction.");
        var old=await LatestAsync(id,ct);
        var e=new ComplaintHandlingEntity {Id=Guid.NewGuid(),ComplaintId=id,Revision=previous.Revision+1,PreviousEventId=old?.Id,
            Operation=operation,FromState=previous.Status,ToState=next,HandlingMode=mode,ResponsibleActorId=responsible,AssignmentEventId=assignment,
            Reason=reason,DetailsJson=ComplaintHandlingRules.CanonicalJson(JsonSerializer.Serialize(details)),
            EvidenceReferencesJson=ComplaintHandlingRules.CanonicalJson(JsonSerializer.Serialize(references)),PublicSummary=summary,
            SubmissionKey=key,InputSha256=hash,AuthorizationGrantId=grant?.GrantId,AuthorizationSourceReference=grant?.SourceReference};
        Events.Add(e);await db.SaveChangesAsync(ct);return e;
    }
    public async Task<HandlingActionResult> ActAsync(Guid id,ComplaintHandlingAction input,bool central,CancellationToken ct)
    {
        if(!policy.WritesEnabled)throw new CentralComplaintUnavailableException();
        // Central permission is checked before complaint existence; DS cannot use this path as a central actor.
        if(central)await policy.RequireReviewAsync(actor.ActorId,input.Operation,ct);
        await RequireScopeAsync(id,central,true,ct);
        if(!central&&!ComplaintHandlingRules.DsOperations.Contains(input.Operation))throw new UnauthorizedAccessException();
        ComplaintHandlingRules.Validate(input);
        var hash=ComplaintRoutingPolicy.Hash(ComplaintHandlingRules.CanonicalJson(JsonSerializer.Serialize(new{id,Input=input})));
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(id,ct);await RequireScopeAsync(id,central,true,ct);
            var grant=central?await policy.RequireReviewAsync(actor.ActorId,input.Operation,ct):null;
            var old=await Events.AsNoTracking().SingleOrDefaultAsync(x=>x.ActorId==actor.ActorId&&x.SubmissionKey==input.SubmissionKey,ct);
            if(old!=null)
            {
                if(old.ComplaintId!=id||old.InputSha256!=hash)throw new ComplaintConcurrencyException();
                var replayState=await CurrentAsync(id,ct);
                await RequireScopeAsync(id,central,true,ct);
                await tx.CommitAsync(ct);return new(old.Id,true,replayState);
            }
            if(await db.Set<ComplaintAssignmentEntity>().AnyAsync(x=>x.AssignedByActorId==actor.ActorId&&x.SubmissionKey==input.SubmissionKey,ct))throw new ComplaintConcurrencyException();
            var state=await CurrentAsync(id,ct);
            if(state.Revision!=input.ExpectedHandlingRevision)throw new ComplaintConcurrencyException();
            if(!(await AllowedAsync(state,central,true,ct)).Contains(input.Operation))throw new ComplaintConcurrencyException();
            if(!central&&!ComplaintHandlingRules.DsOperations.Contains(input.Operation))throw new KeyNotFoundException();
            await ValidateEvidenceAsync(id,input.EvidenceReferences,ct);
            var next=ComplaintHandlingRules.Next(state.Status,input.Operation);
            var mode=ComplaintHandlingRules.Terminal(next)?"None":input.Operation is "TakeDirect" or "Reopen"?"Commissioner":state.Mode;
            var responsible=mode=="None"?(Guid?)null:mode=="Commissioner"&&input.Operation is "TakeDirect" or "Reopen"?actor.ActorId:state.ResponsibleActorId;
            var assignment=mode=="Ds"?state.AssignmentEventId:null;
            var saved=await AppendAsync(id,state,input.Operation,next,mode,responsible,assignment,input.SubmissionKey,hash,input.Reason,input.Details,input.EvidenceReferences,input.PublicSummary,grant,ct);
            if(central)await policy.RequireReviewAsync(actor.ActorId,input.Operation,ct);else if(!await IsAssignedAsync(actor.ActorId,id,ct))throw new KeyNotFoundException();
            await tx.CommitAsync(ct);
            await RequireScopeAsync(id,central,true,ct);
            return new(saved.Id,false,State(saved));
        }
        catch(DbUpdateException e)when(e.InnerException is PostgresException p&&p.SqlState=="23505"){db.ChangeTracker.Clear();throw new ComplaintConcurrencyException();}
    }
    public async Task<HandlingState> PrepareAssignmentAsync(Guid id,AssignComplaint input,CancellationToken ct)
    {
        if(input.ExpectedHandlingRevision==null)throw new ArgumentException("Expected handling revision is required.");
        var state=await CurrentAsync(id,ct);
        if(state.Revision!=input.ExpectedHandlingRevision||ComplaintHandlingRules.Terminal(state.Status))throw new ComplaintConcurrencyException();
        if(state.Mode=="Commissioner")
        {await policy.RequireReviewAsync(actor.ActorId,"AssignDs",ct);if(state.ResponsibleActorId!=actor.ActorId)throw new UnauthorizedAccessException();}
        if(await Events.AnyAsync(x=>x.ActorId==actor.ActorId&&x.SubmissionKey==input.SubmissionKey,ct))throw new ComplaintConcurrencyException();
        return state;
    }
    public async Task RecordAssignmentAsync(HandlingState previous,ComplaintAssignment assignment,AssignComplaint input,CancellationToken ct)
    {
        var grant=await policy.RequireCentralAsync(actor.ActorId,true,ct);
        if(previous.Mode=="Commissioner")await policy.RequireReviewAsync(actor.ActorId,"AssignDs",ct);
        var op=previous.Mode=="Ds"?"ReassignDs":"AssignDs";
        await AppendAsync(assignment.ComplaintId,previous,op,"AssignedToDs","Ds",assignment.TargetActorId,assignment.Id,input.SubmissionKey,
            ComplaintRoutingPolicy.InputHash(assignment.ComplaintId,input),input.Reason,[],[],"Complaint assigned for DS investigation.",grant,ct);
    }
    public async Task RecordReviewAsync(Guid id,HandlingState previous,ComplaintReview review,CancellationToken ct)
    {
        await AppendAsync(id,previous,"DsReviewRecorded",previous.Status,"Ds",previous.ResponsibleActorId,previous.AssignmentEventId,review.Id,
            ComplaintRoutingPolicy.Hash(JsonSerializer.Serialize(new{ComplaintId=id,ReviewId=review.Id})),"Existing DS triage recorded.",
            new(){["reviewId"]=review.Id.ToString("D")},[],"",null,ct);
    }
}
