using Xunit;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class NpdStakeholderApplicabilityTests
{
 [Theory][InlineData(false)][InlineData(true)]
 public void StakeholderMissingIsUnknownButExplicitEmptyRemainsNotApplicable(bool explicitEmpty)
 {
  var rule=NpdRuleCatalogue.GetDefaultRuleDefinitions().Single(r=>r.RuleCode=="NPD-STK-001");
  var input=new ProposalComplianceInput("UNIT-TEST",Stakeholders:explicitEmpty?new StakeholderInput([],[],null,null):null);
  var finding=NpdRuleCatalogue.EvaluateAll(input,[rule]).Single();
  Assert.Equal(explicitEmpty?RuleResultStatus.NotApplicable:RuleResultStatus.InsufficientInformation,finding.Status);
  Assert.Equal(explicitEmpty?RuleApplicability.NotApplicable:RuleApplicability.Undetermined,finding.Applicability);
  Assert.False(finding.IsBlocking);
 }
 [Fact]
 public void MissingExpectedListRemainsInsufficientEvenWhenConsultedListExists()
 {
  var rule=NpdRuleCatalogue.GetDefaultRuleDefinitions().Single(r=>r.RuleCode=="NPD-STK-001");
  var input=new ProposalComplianceInput("UNIT-TEST",Stakeholders:new StakeholderInput(null,["UNIT-TEST-REVIEWER"],null,null));
  var finding=NpdRuleCatalogue.EvaluateAll(input,[rule]).Single();
  Assert.Equal(RuleResultStatus.InsufficientInformation,finding.Status);
  Assert.Equal(RuleApplicability.Undetermined,finding.Applicability);
  Assert.True(finding.RequiresHumanReview);
 }
}
