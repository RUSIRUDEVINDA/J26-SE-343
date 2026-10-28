using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

internal static class IndividualFindingClosureProof
{
    public static async Task RunAsync(string connection, Func<Task<WebApplication>> host,
        Func<WebApplication, Guid?, string, HttpClient> client, Guid ds, Guid other, Guid commissioner, Action<bool,string> check)
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(connection).Options;
        var loc = new ComplianceFindingDto("NPD-LOC-001","1.0","Completeness","Applicable","InsufficientInformation","Medium",false,true,
            "Fictional saved incomplete input","Full project location hierarchy (Province, District, DSD, GND) must be specified.","Missing","MissingInputs",
            "Department of National Planning","Operational Manual / Project Submission Format (January 2019)","Item 3 — Project Location","Complete missing location hierarchy fields.");
        var rationale = loc with {RuleCode="NPD-RAT-001",Status="RequiresHumanReview",ExpectedRequirement="Project rationale must be based on identified problems/needs.",SourceSection="Item 7 — Rationale of Project"};
        var legal = loc with {RuleCode="NPD-SOC-001",RuleVersion="9.7",Category="SocialSafeguard",Status="RequiresHumanReview",IsBlocking=true,ExpectedRequirement="Independent legal approval is required."};
        var original = new[]{loc,rationale,legal};
        async Task<Guid> Seed()
        {
            await using var db = new GovernanceIntelligenceDbContext(options);var id=Guid.NewGuid();var audit=Guid.NewGuid();
            db.GovernanceAuditRecords.Add(new(){Id=audit,EngineType=0,ActionName="Isolated individual closure",Status="Saved",Details="Fictional Step12 fixture",Timestamp=DateTime.UtcNow});
            db.ComplianceEvaluations.Add(new(){Id=id,ProposalId="isolated-step12-"+id,AuditRecordId=audit,ActionName="Isolated individual closure",Status="InsufficientInformation",RuleSetVersion="fixture/12",FindingsJson=JsonSerializer.Serialize(original,RegulatoryJson.Options),EvaluationTimestamp=DateTime.UtcNow});
            var key=Guid.NewGuid();db.Set<RegulatoryEvaluationProvenanceEntity>().Add(new(){EvaluationId=id,CreatorActorId=ds,SubmissionKey=key,SubmissionHash=new string('a',64),ProvenanceJson=JsonSerializer.Serialize(new SavedRegulatoryProvenance(ds,key,new string('a',64),"{}",new string('b',64),"fixture/12","c4-regulatory-input/1","Isolated officer closure fixture",new string('c',64),"fixture/12",null),RegulatoryJson.Options)});
            await db.SaveChangesAsync();return id;
        }
        string Route(Guid id)=>$"/api/governance-intelligence/regulatory-assessments/{id}/follow-ups";
        var evaluation=await Seed();var raceEvaluation=await Seed();var referredEvaluation=await Seed();
        var input = new RecordRegulatoryFollowup(Guid.NewGuid(),loc.RuleCode,FindingClosurePolicy.Operation,null,null,
            "",
            [],0);
        Guid closure;
        await using(var app=await host())
        {
            using var owner=client(app,ds,"Officer");using var foreign=client(app,other,"Officer");using var central=client(app,commissioner,"Officer");using var applicant=client(app,Guid.NewGuid(),"Applicant");using var anonymous=client(app,null,"Officer");
            var route=Route(evaluation);
            var before=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route))!;
            check(before.ClosureEligibility!.Count(x=>x.CanClose)==3,"Backend exposes exact per-rule closure eligibility and includes arbitrary versions and blocking officer dispositions");
            foreach(var pair in new[]{(Client:foreign,Status:HttpStatusCode.NotFound),(Client:central,Status:HttpStatusCode.NotFound),(Client:applicant,Status:HttpStatusCode.Forbidden),(Client:anonymous,Status:HttpStatusCode.Unauthorized)})
                check((await pair.Client.PostAsJsonAsync(route,input)).StatusCode==pair.Status,"Closure authorization denies "+pair.Status);
            foreach(var bad in new[]{input with{Remarks=new string('x',2001)},input with{References=new string[11]},input with{ReviewOutcome="NoAdditionalConcern"},input with{RuleCode="NO-SUCH-RULE"}})
                check((await owner.PostAsJsonAsync(route,bad)).StatusCode==HttpStatusCode.BadRequest,"Closure rejects malformed payload or nonexistent saved rule");
            var response=await owner.PostAsJsonAsync(route,new{input.SubmissionKey,input.RuleCode,input.Operation,input.ExpectedRevision});check(response.StatusCode==HttpStatusCode.OK,"Insufficient Information closure saved");
            var saved=(await response.Content.ReadFromJsonAsync<RegulatoryFollowupSaved>())!;closure=saved.Event.Id;
            check(saved.Event.ActorId==ds&&saved.Event.Revision==1&&saved.Event.CreatedAtUtc!=default&&saved.Event.RuleVersion==loc.RuleVersion&&saved.Event.References.Length==0&&saved.Event.Remarks=="","Closure binds officer timestamp rule version references and revision");
            var replay=(await (await owner.PostAsJsonAsync(route,input)).Content.ReadFromJsonAsync<RegulatoryFollowupSaved>())!;
            check(replay.Reused&&replay.Event.Id==closure,"Exact closure replay returns same event without duplicate");
            check((await owner.PostAsJsonAsync(route,input with{SubmissionKey=Guid.NewGuid()})).StatusCode==HttpStatusCode.Conflict,"Stale closure revision rejected");
            check((await owner.PostAsJsonAsync(route,input with{SubmissionKey=Guid.NewGuid(),ExpectedRevision=1})).StatusCode==HttpStatusCode.BadRequest,"New-key duplicate closure rejected even with current revision");
            check((await owner.PostAsJsonAsync(route,input with{Remarks=input.Remarks+" Changed."})).StatusCode==HttpStatusCode.Conflict,"Changed-content closure replay rejected");
            var after=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route))!;
            check(after.History.Count==1&&after.History.All(x=>x.RuleCode==loc.RuleCode),"Closing one finding leaves every other stream unchanged");
            check(after.ClosureEligibility!.Single(x=>x.RuleCode==rationale.RuleCode).CanClose,"Unrelated review remains eligible and unresolved");
            check(RegulatoryProgression.Holds(original,after.History).Length==2,"Only matching closed finding releases its progression hold");
            check((await owner.PostAsJsonAsync(route,input with{SubmissionKey=Guid.NewGuid(),RuleCode=rationale.RuleCode})).StatusCode==HttpStatusCode.OK,"Eligible Human Review Required finding closed independently");
            var both=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route))!;
            check(both.History.Count==2&&RegulatoryProgression.Holds(original,both.History).Single().Contains(legal.RuleCode),"Mandatory external blocking finding remains unresolved after two closures");
            check((await owner.PostAsJsonAsync(route,input with{SubmissionKey=Guid.NewGuid(),RuleCode=legal.RuleCode})).StatusCode==HttpStatusCode.OK,"Arbitrary-version blocking Human Review finding records officer disposition without text/evidence");
            var all=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route))!;
            check(all.History.Count==3&&RegulatoryProgression.Holds(original,all.History).Length==0,"All three closures resolve C4 screening without recording external approval");
            var review=input with{SubmissionKey=Guid.NewGuid(),Operation="HumanReviewRecorded",ReviewOutcome="FurtherInstitutionalLegalReview",References=[],Remarks="Independent institutional legal review remains required."};
            check((await owner.PostAsJsonAsync(Route(referredEvaluation),review)).StatusCode==HttpStatusCode.OK,"Independent institutional review referral saved");
            var denied=await owner.PostAsJsonAsync(Route(referredEvaluation),input with{SubmissionKey=Guid.NewGuid(),ExpectedRevision=1});
            check(denied.StatusCode==HttpStatusCode.OK,"Officer disposition can be recorded alongside unresolved institutional review");
            var referred=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(Route(referredEvaluation)))!;
            check(!RegulatoryProgression.Holds(original,referred.History).Any(x=>x.Contains(loc.RuleCode)),"Later institutional approval is not a C4 gate after the saved DS closure");
            var race=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>owner.PostAsJsonAsync(Route(raceEvaluation),input with{SubmissionKey=Guid.NewGuid()})));
            check(race.Count(x=>x.StatusCode==HttpStatusCode.OK)==1&&race.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent closures persist exactly one winner");
            await app.StopAsync();
        }
        await using(var app=await host())
        {
            using var owner=client(app,ds,"Officer");var reread=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(Route(evaluation)))!;
            check(reread.History.Count==3&&reread.History.Any(x=>x.Id==closure),"Restart and refreshed reads retain exact closure events");await app.StopAsync();
        }
        await using(var db=new GovernanceIntelligenceDbContext(options))
        {
            var row=await db.ComplianceEvaluations.SingleAsync(x=>x.Id==evaluation);
            check(row.Status=="InsufficientInformation"&&JsonSerializer.Deserialize<ComplianceFindingDto[]>(row.FindingsJson!,RegulatoryJson.Options)!.SequenceEqual(original)&&row.RuleSetVersion=="fixture/12","Original evaluation result every finding and rule set unchanged");
            foreach(var e in await db.Set<RegulatoryFollowupEntity>().Where(x=>x.EvaluationId==evaluation).ToArrayAsync())
                check(e.Canonical(RegulatoryFollowupInput.FindingHash(original.Single(f=>f.RuleCode==e.RuleCode)))==e.CanonicalJson&&RegulatoryJson.Hash(e.CanonicalJson)==e.EventSha256,"Closure audit canonical hash preserved");
        }
    }
}
