using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
public sealed class OfficerFlowStore(GovernanceIntelligenceDbContext db,IConfiguration config,TimeProvider clock,IComponent3WorkflowGovernanceProjectionStore projections,IWorkflowAnomalyAssessmentStore anomalies,IEarlyGovernanceScreeningStore earlyStore,ScreenEarlyGovernanceCommandHandler early,AssessWorkflowAnomalyCommandHandler anomaly,ICommissionerReportReadAccess? commissioner=null,IInvestigationEvidenceStore? investigationEvidence=null):IOfficerFlowStore {
 static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 DbSet<OfficerInvestigationEntity> Investigations=>db.Set<OfficerInvestigationEntity>();
 DbSet<OfficerAssessmentRequestEntity> Requests=>db.Set<OfficerAssessmentRequestEntity>();
 static void Actor(Guid actor){if(actor==Guid.Empty)throw new UnauthorizedAccessException();}
 async Task WriteReady(string kind,CancellationToken ct){
  if(!config.GetValue<bool>("GovernanceIntelligence:KiwulaWorkspace:WritesEnabled")||(kind=="anomaly"&&(!config.GetValue<bool>("GovernanceIntelligence:PostWorkflowDemonstrations:Enabled")||!config.GetValue<bool>("GovernanceIntelligence:PostWorkflowDemonstrations:WritesEnabled"))))throw new OfficerUnavailableException();
  if(!await db.Database.SqlQueryRaw<bool>("SELECT current_setting('transaction_read_only')='off' AND has_table_privilege(current_user,'governance_intelligence.officer_investigations','INSERT') AND has_table_privilege(current_user,'governance_intelligence.officer_assessment_requests','INSERT') AND has_table_privilege(current_user,'governance_intelligence.officer_action_events','INSERT') AND has_column_privilege(current_user,'governance_intelligence.officer_assessment_requests','State','UPDATE') AND has_column_privilege(current_user,'governance_intelligence.officer_assessment_requests','FinishedAtUtc','UPDATE') AND has_column_privilege(current_user,'governance_intelligence.officer_assessment_requests','EarlyAssessmentId','UPDATE') AND has_column_privilege(current_user,'governance_intelligence.officer_assessment_requests','AnomalyAssessmentId','UPDATE') AND has_column_privilege(current_user,'governance_intelligence.officer_assessment_requests','FailureCode','UPDATE') AND has_column_privilege(current_user,'governance_intelligence.officer_assessment_requests','Revision','UPDATE') AND has_table_privilege(current_user,'governance_intelligence.early_governance_screening_evaluations','INSERT') AND has_table_privilege(current_user,'governance_intelligence.component3_workflow_projections','INSERT') AND has_table_privilege(current_user,'governance_intelligence.workflow_anomaly_assessments','INSERT') AS \"Value\"").SingleAsync(ct))throw new OfficerUnavailableException();
 }
 public async Task<bool> ReadyAsync(CancellationToken ct){try{await WriteReady("anomaly",ct);return true;}catch(OperationCanceledException){throw;}catch{return false;}}
 // Parameterized, transaction-scoped lock is confined to this repository; no lock spans HTTP.
 Task Lock(string value,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({value},0))",ct);
 void Event(Guid actor,Guid? investigation,Guid? request,string operation,object data){
  var id=Guid.NewGuid();var at=clock.GetUtcNow();var canonical=JsonSerializer.Serialize(new{id,actor,investigation,request,operation,at,data},Json);
  db.Set<OfficerActionEventEntity>().Add(new(){Id=id,CreatorActorId=actor,InvestigationId=investigation,RequestId=request,Operation=operation,CreatedAtUtc=at,DetailsJson=JsonSerializer.Serialize(new{canonical},Json),EventSha256=OfficerFlowInput.Hash(canonical)});
 }
 static Investigation Read(OfficerInvestigationEntity row)=>new(row.Id,row.CreatorActorId,row.ProposalId,row.SubmissionKey,row.SourceVersion,row.InputSha256,JsonSerializer.Deserialize<OfficerFinding[]>(row.FindingsJson,Json)!,row.CreatedAtUtc,row.SupersedesId);
 public async Task<Investigation> InvestigationAsync(Guid actor,Guid id,CancellationToken ct){Actor(actor);var row=await Investigations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException();if(row.CreatorActorId!=actor)throw new UnauthorizedAccessException();return Read(row);}
 public async Task<IReadOnlyList<Investigation>> InvestigationsAsync(Guid actor,CancellationToken ct){Actor(actor);return (await Investigations.AsNoTracking().Where(x=>x.CreatorActorId==actor).OrderByDescending(x=>x.CreatedAtUtc).Take(100).ToListAsync(ct)).Select(Read).ToArray();}
 public async Task<Investigation> SaveAsync(Guid actor,SaveInvestigation input,CancellationToken ct){
  Actor(actor);input=OfficerFlowInput.Validate(input);await WriteReady("early",ct);var json=JsonSerializer.Serialize(input.Findings,Json);var hash=OfficerFlowInput.Hash(JsonSerializer.Serialize(new{input.ProposalId,input.Findings,input.SupersedesId,version=OfficerFlowInput.Version},Json));
  await using var tx=await db.Database.BeginTransactionAsync(ct);await Lock(actor+":"+input.SubmissionKey,ct);
  var old=await Investigations.AsNoTracking().SingleOrDefaultAsync(x=>x.CreatorActorId==actor&&x.SubmissionKey==input.SubmissionKey,ct);if(old!=null){if(old.InputSha256!=hash)throw new OfficerConflictException();return Read(old);}
  if(input.SupersedesId is {} parent){var saved=await InvestigationAsync(actor,parent,ct);if(saved.ProposalId!=input.ProposalId)throw new OfficerConflictException();}
  var row=new OfficerInvestigationEntity{Id=Guid.NewGuid(),CreatorActorId=actor,ProposalId=input.ProposalId,SubmissionKey=input.SubmissionKey,SourceVersion=OfficerFlowInput.Version,InputSha256=hash,FindingsJson=json,CreatedAtUtc=clock.GetUtcNow(),SupersedesId=input.SupersedesId};Investigations.Add(row);Event(actor,row.Id,null,"InvestigationSaved",new{row.InputSha256,row.SourceVersion});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await InvestigationAsync(actor,row.Id,ct);
 }
 public Task<OfficerAssessment> EarlyAsync(Guid actor,EarlyRequest input,CancellationToken ct)=>Run(actor,"early",input.SubmissionKey,input.InvestigationId,null,ct);
 public Task<OfficerAssessment> AnomalyAsync(Guid actor,AnomalyRequest input,CancellationToken ct){if(input.Scenario!=NamedOfficerWorkflow.Scenario)throw new ArgumentException("Unknown server scenario.");return Run(actor,"anomaly",input.SubmissionKey,null,input.Scenario,ct);}
 async Task<OfficerAssessment> Run(Guid actor,string kind,Guid key,Guid? investigationId,string? scenario,CancellationToken ct){
  Actor(actor);if(key==Guid.Empty)throw new ArgumentException("A submission key is required.");await WriteReady(kind,ct);
  var investigation=investigationId is {} inv?await InvestigationAsync(actor,inv,ct):null;
  string scope=investigation?.ProposalId??scenario!,version=investigation?.SourceVersion??NamedOfficerWorkflow.Version;
  OfficerAssessmentRequestEntity row;
  await using(var tx=await db.Database.BeginTransactionAsync(ct)){
   // Same actor/key is global to both request kinds; scope lock additionally excludes competing keys.
   await Lock(actor+":"+key,ct);await Lock(actor+":"+kind+":"+scope+":"+version,ct);
   var old=await Requests.AsNoTracking().SingleOrDefaultAsync(x=>x.CreatorActorId==actor&&x.SubmissionKey==key,ct);
   if(old!=null){if(old.Kind!=kind||old.InvestigationId!=investigationId||old.Scenario!=scenario||old.SourceVersion!=version)throw new OfficerConflictException();return await GetAsync(actor,old.Id,ct);}
   if(kind=="early"){if(investigationEvidence==null)throw new OfficerUnavailableException();await investigationEvidence.RequireFinalizedAsync(actor,investigationId!.Value,ct);}
   if(await Requests.AnyAsync(x=>x.CreatorActorId==actor&&x.Kind==kind&&x.ScopeKey==scope&&x.SourceVersion==version&&(x.State=="Pending"||x.State=="Unknown"),ct))throw new OfficerConflictException();
   row=new(){Id=Guid.NewGuid(),CreatorActorId=actor,SubmissionKey=key,Kind=kind,ScopeKey=scope,SourceVersion=version,InvestigationId=investigationId,Scenario=scenario,CreatedAtUtc=clock.GetUtcNow(),InputSha256=investigation?.InputSha256??""};
   if(kind=="anomaly"){
    row.WorkflowRunId=Guid.NewGuid();row.ProjectionId=Guid.NewGuid();var projection=NamedOfficerWorkflow.Create(row.WorkflowRunId.Value,row.ProjectionId.Value,row.CreatedAtUtc);row.InputSha256=projection.InputSha256;await projections.UpsertAsync(projection,ct);
   }
   Requests.Add(row);Event(actor,null,row.Id,"RequestSubmitted",new{kind,version,row.InputSha256});if(kind=="anomaly")Event(actor,null,row.Id,"InferenceStarted",new{row.ProjectionId});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  }
  Guid? earlyId=null,anomalyId=null;string state="Saved";string? failure=null;
  try {
   if(kind=="early"){
    earlyId=Guid.NewGuid();await early.HandleAsync(new(earlyId.Value,row.Id,KiwulaCatalogue.Prefix+row.Id,version,OfficerFlowInput.Indicators(investigation!)),ct);
   } else {
    var result=await anomaly.HandleAsync(AssessWorkflowAnomalyCommand.FromProjection(row.ProjectionId!.Value,PostWorkflowDemonstrations.CasePrefix+row.WorkflowRunId,row.WorkflowRunId!.Value),ct);
    if(!result.IsSuccess||result.Assessment==null){state="Unknown";failure="ML_OUTCOME_UNCONFIRMED";}else anomalyId=result.Assessment.AssessmentId;
   }
  }catch(Exception){state="Unknown";failure="PROCESSING_OUTCOME_UNCONFIRMED";earlyId=null;anomalyId=null;db.ChangeTracker.Clear();}
  // Failure/cancellation is recorded with an independent bounded token; never retry inference.
  using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(10));var finish=cleanup.Token;
  await using(var tx=await db.Database.BeginTransactionAsync(finish)){
   var current=await Requests.SingleAsync(x=>x.Id==row.Id,finish);if(current.State!="Pending"||current.Revision!=1)throw new OfficerConflictException();
   current.State=state;current.EarlyAssessmentId=state=="Saved"?earlyId:null;current.AnomalyAssessmentId=state=="Saved"?anomalyId:null;current.FailureCode=failure;current.FinishedAtUtc=clock.GetUtcNow();current.Revision++;
   Event(actor,null,row.Id,state=="Saved"?"AssessmentSaved":"OutcomeUnknown",new{earlyId,anomalyId,failure});await db.SaveChangesAsync(finish);await tx.CommitAsync(finish);
  }
  return await GetAsync(actor,row.Id,finish);
 }
 public async Task<OfficerAssessment> GetAsync(Guid actor,Guid id,CancellationToken ct){
  Actor(actor);var r=await Requests.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException();if(r.CreatorActorId!=actor&&!(r.State=="Saved"&&commissioner!=null&&await commissioner.CanReadAllAsync(actor,ct)))throw new UnauthorizedAccessException();
  object? result=r.EarlyAssessmentId is {} e?(object?)await earlyStore.GetByIdAsync(e,ct):r.AnomalyAssessmentId is {} a?await anomalies.GetByIdAsync(a,ct):null;
  var projection=r.ProjectionId is {} p?await projections.GetByIdAsync(p,ct):null;
  var events=await db.Set<OfficerActionEventEntity>().AsNoTracking().Where(x=>x.RequestId==id&&x.CreatorActorId==r.CreatorActorId).OrderBy(x=>x.CreatedAtUtc).ToListAsync(ct);
  if(r.CreatorActorId!=actor&&!await commissioner!.CanReadAllAsync(actor,ct))throw new UnauthorizedAccessException();
  return new(r.Id,r.CreatorActorId,r.Kind,r.ScopeKey,r.SourceVersion,r.InputSha256,r.State,r.CreatedAtUtc,r.FinishedAtUtc,r.InvestigationId,r.Scenario,r.WorkflowRunId,r.ProjectionId,r.EarlyAssessmentId,r.AnomalyAssessmentId,r.FailureCode,r.Revision,result,projection,events.Cast<object>().ToArray());
 }
 public async Task<IReadOnlyList<OfficerAssessment>> HistoryAsync(Guid actor,CancellationToken ct){Actor(actor);var all=commissioner!=null&&await commissioner.CanReadAllAsync(actor,ct);var ids=await Requests.AsNoTracking().Where(x=>x.CreatorActorId==actor||all&&x.State=="Saved").OrderByDescending(x=>x.CreatedAtUtc).Take(100).Select(x=>x.Id).ToListAsync(ct);var rows=new List<OfficerAssessment>();foreach(var id in ids)rows.Add(await GetAsync(actor,id,ct));return rows;}
}
