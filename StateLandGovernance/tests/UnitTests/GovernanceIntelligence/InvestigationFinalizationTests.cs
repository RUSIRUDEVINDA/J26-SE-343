using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class InvestigationFinalizationTests {
 static Investigation Snapshot(string choice="NoIssueFound")=>new(Guid.NewGuid(),Guid.NewGuid(),"test",Guid.NewGuid(),"test/1",new string('a',64),Enum.GetNames<EarlyGovernanceIndicatorType>().Select(c=>new OfficerFinding(c,choice,"Meaningful officer observation for this concern.")).ToArray(),DateTimeOffset.UtcNow,null);
 [Fact]public void Finalization_SevenNegativeFindings_NoFilesRequired()=>InvestigationFinalizationInput.Validate(Snapshot(),[]);
 [Fact]public void Finalization_MixedFindings_ExactConcernEvidenceRequired(){var s=Snapshot();var f=s.Findings.ToArray();f[0]=f[0] with{Choice="IssueFound"};s=s with{Findings=f};Assert.Throws<ArgumentException>(()=>InvestigationFinalizationInput.Validate(s,[]));Assert.Throws<ArgumentException>(()=>InvestigationFinalizationInput.Validate(s,[f[1].Concern]));InvestigationFinalizationInput.Validate(s,[f[0].Concern]);}
 [Theory][InlineData("NotYetVerified")][InlineData("")][InlineData("Verified")]
 public void Finalization_UntouchedOrUnsupportedFinding_Rejected(string choice)=>Assert.Throws<ArgumentException>(()=>InvestigationFinalizationInput.Validate(Snapshot(choice),[]));
 [Fact]public void Finalization_DuplicateOrMissingConcern_Rejected(){var s=Snapshot();Assert.Throws<ArgumentException>(()=>InvestigationFinalizationInput.Validate(s with{Findings=s.Findings.Take(6).ToArray()},[]));Assert.Throws<ArgumentException>(()=>InvestigationFinalizationInput.Validate(s with{Findings=Enumerable.Repeat(s.Findings[0],7).ToArray()},[]));}
 [Fact]public void Finalization_DoesNotPromoteEngineEvidence(){var s=Snapshot();InvestigationFinalizationInput.Validate(s,[]);Assert.All(OfficerFlowInput.Indicators(s),i=>Assert.Equal("Unverified",i.EvidenceState));}
 [Fact]public void Finalization_PreservesHistoricalDraftMapping(){var s=Snapshot("NotYetVerified");s=s with{Findings=s.Findings.Select(f=>f with{Remarks=""}).ToArray()};Assert.All(OfficerFlowInput.Indicators(s),i=>Assert.Equal("Missing",i.EvidenceState));}
}
