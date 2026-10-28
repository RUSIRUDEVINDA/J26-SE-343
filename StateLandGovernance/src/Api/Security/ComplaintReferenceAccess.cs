using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
namespace StateLandGovernance.Api.Security;
// Composition-root peer adapter. Reads C3 assignments/cases; never changes their ownership or workflow.
public sealed class ComplaintReferenceAccess(ILeaseCaseRepository cases,ICaseAssignmentStore assignments,
    GovernanceIntelligenceDbContext c4,TimeProvider time):IComplaintReferenceAccess
{
    public async Task<ComplaintReference> ValidateAsync(string? reference,string? parcel,CancellationToken ct){
        Guid? caseId=null;
        if(!string.IsNullOrWhiteSpace(reference)){
            var row=Guid.TryParse(reference,out var id)&&id!=Guid.Empty?await cases.GetByIdAsync(new LeaseCaseId(id),ct):await cases.GetByApplicationReferenceAsync(reference,ct);
            if(row==null)throw new ArgumentException("The case reference could not be validated.");caseId=row.Id.Value;
        }
        if(!string.IsNullOrWhiteSpace(parcel)){
            var valid=await c4.Component1CaseProjections.AsNoTracking().AnyAsync(x=>x.LandParcelId==parcel && (caseId==null||x.CaseId==caseId.ToString()),ct);
            if(!valid)throw new ArgumentException("The parcel reference could not be validated against available C1 projections. Leave it blank if unknown.");
        }
        return new(caseId,string.IsNullOrWhiteSpace(parcel)?null:parcel);
    }
    public async Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor,CancellationToken ct){
        var now=time.GetUtcNow();var rows=await assignments.ListAsync(null,actor,null,false,ct);
        return rows.Where(x=>x.Assignment.Active is {} g && g.ActorId==actor&&g.RevokedAt==null&&g.ValidFrom<=now&&g.ValidUntil>now&&g.Capabilities.Contains("CaseWorkspaceReviewer")).Select(x=>x.CaseId).Distinct().ToArray();
    }
}
