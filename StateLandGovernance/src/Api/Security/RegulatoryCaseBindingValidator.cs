using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
namespace StateLandGovernance.Api.Security;
public sealed class RegulatoryCaseBindingValidator(ILeaseCaseRepository cases,IDocumentAnalysisRepository analyses,IComplaintReferenceAccess access):IRegulatoryCaseBindingValidator{
 public async Task ValidateAsync(Guid actor,RegulatoryCaseBinding? binding,CancellationToken ct){
  if(binding==null)return;
  if(binding.CaseId==Guid.Empty||!(await access.ReviewerCasesAsync(actor,ct)).Contains(binding.CaseId))throw new UnauthorizedAccessException();
  var row=await cases.GetByIdAsync(new LeaseCaseId(binding.CaseId),ct)??throw new ArgumentException("C3 case does not exist.");
  if(binding.SnapshotId!=null){
   if(binding.SnapshotId==Guid.Empty||binding.ProposalVersion==null||binding.ProposalVersion==Guid.Empty||row.CurrentVerifiedFactSnapshotId!=binding.SnapshotId)throw new ArgumentException("C3 snapshot/proposal binding is missing or stale.");
   var snapshot=await analyses.GetSnapshotByIdAsync(new VerifiedFactSnapshotId(binding.SnapshotId.Value),ct);
   if(snapshot==null||snapshot.DocumentVersionId.Value!=binding.ProposalVersion)throw new ArgumentException("C3 snapshot does not bind this proposal version.");
  }else if(binding.ProposalVersion!=null)throw new ArgumentException("Supply the linked snapshot with the proposal version.");
 }
}
