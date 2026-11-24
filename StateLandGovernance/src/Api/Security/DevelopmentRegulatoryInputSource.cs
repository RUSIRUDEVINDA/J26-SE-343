using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
namespace StateLandGovernance.Api.Security;
public sealed class DevelopmentRegulatoryInputSource(IConfiguration config,IHostEnvironment env,UploadedCaseResearchScenarios scenarios) : IDevelopmentRegulatoryInputSource
{
 public sealed record Envelope(Guid CaseId,Guid SnapshotId,Guid ProposalVersion,string Version,string SourceReference,JsonElement Input);
 public async Task<DevelopmentRegulatoryInput?> ReadAsync(Guid caseId,Guid snapshotId,Guid proposalVersion,CancellationToken ct)
 {
  if(!env.IsDevelopment() || !config.GetValue<bool>("GovernanceIntelligence:DevelopmentCatalogue:Enabled")) return null;
  if(!await scenarios.CanUseAsync(caseId,ct)) throw new UnauthorizedAccessException("An assigned, designated synthetic Proposal is required.");
  var path=config["GovernanceIntelligence:DevelopmentCatalogue:InputPath"];
  if(string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("A server-owned structured Development input file is required.");
  var value=JsonSerializer.Deserialize<Envelope>(await File.ReadAllTextAsync(path,ct),new JsonSerializerOptions(JsonSerializerDefaults.Web));
  return Bind(value,caseId,snapshotId,proposalVersion);
 }
 // Pure validation seam: caller must first establish the existing designation/assignment guard.
 public static DevelopmentRegulatoryInput Bind(Envelope? value,Guid caseId,Guid snapshotId,Guid proposalVersion)
 {
  if(caseId==Guid.Empty || snapshotId==Guid.Empty || proposalVersion==Guid.Empty ||
     value==null || value.CaseId!=caseId || value.SnapshotId!=snapshotId || value.ProposalVersion!=proposalVersion ||
     string.IsNullOrWhiteSpace(value.Version)||string.IsNullOrWhiteSpace(value.SourceReference)||value.Input.ValueKind!=JsonValueKind.Object ||
     !value.Input.TryGetProperty("proposalId",out var proposalId) || proposalId.ValueKind!=JsonValueKind.String || proposalId.GetString()!=caseId.ToString("D"))
   throw new InvalidOperationException("Development input case/snapshot/Proposal version or provenance does not match.");
  var dto=JsonSerializer.Deserialize<StateLandGovernance.GovernanceIntelligence.Application.DTOs.ProposalComplianceInputDto>(value.Input.GetRawText(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
  _=StateLandGovernance.GovernanceIntelligence.Application.DTOs.ProposalComplianceInputMapper.Map(dto,strictDemonstrationInput:true);
  return new(value.Version,value.SourceReference,value.Input.GetRawText());
 }
}
