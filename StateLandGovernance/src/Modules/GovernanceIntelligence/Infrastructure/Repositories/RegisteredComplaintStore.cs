using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
public sealed class RegisteredComplaintStore(GovernanceIntelligenceDbContext db,IComplaintClassificationAssessmentStore assessments):IRegisteredComplaintStore
{
    private DbSet<RegisteredComplaintEntity> Rows=>db.Set<RegisteredComplaintEntity>();
    private static RegisteredComplaint? Read(RegisteredComplaintEntity? row)=>row==null?null:JsonSerializer.Deserialize<RegisteredComplaint>(row.PayloadJson);
    private static void Set(RegisteredComplaintEntity entity,RegisteredComplaint row){entity.Id=row.Id;entity.ApplicantId=row.ApplicantId;entity.SubmissionKey=row.SubmissionKey;entity.CaseId=row.CaseId;entity.SubmittedAtUtc=row.SubmittedAtUtc;entity.Revision=row.Revision;entity.PayloadJson=JsonSerializer.Serialize(row);}
    public async Task<RegisteredComplaint?> GetAsync(Guid id,CancellationToken ct)=>Read(await Rows.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct));
    public async Task<RegisteredComplaint?> FindSubmissionAsync(Guid actor,Guid key,CancellationToken ct)=>Read(await Rows.AsNoTracking().SingleOrDefaultAsync(x=>x.ApplicantId==actor&&x.SubmissionKey==key,ct));
    public async Task<IReadOnlyList<RegisteredComplaint>> ListAsync(Guid? applicant,IReadOnlyList<Guid> cases,CancellationToken ct){
        var rows=await Rows.AsNoTracking().Where(x=>applicant!=null?x.ApplicantId==applicant:x.CaseId!=null&&cases.Contains(x.CaseId.Value)).OrderByDescending(x=>x.SubmittedAtUtc).ThenBy(x=>x.Id).Take(100).ToArrayAsync(ct);
        return rows.Select(x=>Read(x)!).ToArray();
    }
    public async Task<bool> InsertAsync(RegisteredComplaint complaint,CancellationToken ct){
        var row=new RegisteredComplaintEntity();Set(row,complaint);Rows.Add(row);
        try{await db.SaveChangesAsync(ct);return true;}catch(DbUpdateException e)when(e.InnerException is PostgresException p && p.SqlState=="23505") {db.ChangeTracker.Clear();return false;}
    }
    public async Task SaveAsync(RegisteredComplaint complaint,int revision,ComplaintClassificationAssessment? assessment,CancellationToken ct){
        await using var tx=db.Database.IsRelational() && db.Database.CurrentTransaction==null?await db.Database.BeginTransactionAsync(ct):null;
        try{
            var entity=await Rows.SingleAsync(x=>x.Id==complaint.Id,ct);
            if(entity.Revision!=revision)throw new ComplaintConcurrencyException();
            if(entity.ApplicantId!=complaint.ApplicantId || entity.SubmissionKey!=complaint.SubmissionKey || entity.CaseId!=complaint.CaseId)
                throw new InvalidOperationException("Complaint identity and reference are immutable.");
            var original=Read(entity)!;
            if(original.LandAddress!=complaint.LandAddress || original.UnverifiedCaseReference!=complaint.UnverifiedCaseReference ||
               original.UnverifiedParcelReference!=complaint.UnverifiedParcelReference || original.ParcelId!=complaint.ParcelId)
                throw new InvalidOperationException("Original complaint location and references are immutable.");
            // PostgreSQL stores timestamps at microsecond precision, while the original JSON
            // may retain .NET ticks. Never rewrite immutable indexed fields during CAS saves.
            db.Entry(entity).Property(x=>x.Revision).OriginalValue=revision;
            entity.Revision=complaint.Revision;entity.PayloadJson=JsonSerializer.Serialize(complaint);
            await db.SaveChangesAsync(ct);
            if(assessment!=null){await assessments.AddAsync(assessment,ct);db.Set<RegisteredComplaintAssessmentLinkEntity>().Add(new(){ComplaintId=complaint.Id,AssessmentId=assessment.AssessmentId});await db.SaveChangesAsync(ct);}
            if(tx!=null)await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }catch(DbUpdateConcurrencyException){db.ChangeTracker.Clear();throw new ComplaintConcurrencyException();}
    }
    public async Task<Guid?> ComplaintForAssessmentAsync(Guid id,CancellationToken ct)=>await db.Set<RegisteredComplaintAssessmentLinkEntity>().Where(x=>x.AssessmentId==id).Select(x=>(Guid?)x.ComplaintId).SingleOrDefaultAsync(ct);
}
