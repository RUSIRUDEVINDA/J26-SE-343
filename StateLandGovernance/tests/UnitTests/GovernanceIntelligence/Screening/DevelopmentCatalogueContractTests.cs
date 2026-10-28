using Xunit;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Fixtures;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence.Screening;
public sealed class DevelopmentCatalogueContractTests
{
 [Fact] public void StructuredInputsAndPolicyVersionsHaveDifferentCacheKeys()
 {
  var original=C4DevelopmentCaseFixtureProvider.GetEcoTourismResortFixture();
  var first=original with {DevelopmentRegulatoryInput=new ProposalComplianceInputDto(original.CaseId),DevelopmentRegulatoryInputVersion="UNIT-TEST-1"};
  Assert.NotEqual(OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(original),OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(first));
  Assert.NotEqual(OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(first),OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(first with {DevelopmentRegulatoryInputVersion="UNIT-TEST-2"}));
  Assert.NotEqual(OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(first),OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(first with {EffectivePolicyVersion="UNIT-TEST-OTHER"}));
  Assert.NotEqual(OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(first),OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(first with {DevelopmentRegulatoryInput=first.DevelopmentRegulatoryInput! with {Financing=new FinancingDto(RevenueExpected:true)}}));
 }
 [Fact] public void MapperPreservesUnknownsAndExplicitEmptyStakeholders()
 {
  var dto=new ProposalComplianceInputDto("UNIT-TEST");
  var mapped=ProposalComplianceInputMapper.Map(dto,strictDemonstrationInput:true);
  Assert.Null(mapped.Stakeholders);Assert.Null(mapped.Financing);Assert.Null(mapped.ImpactAssessment);
 }
 [Fact] public void DemonstrationCashFlowsCannotAssumeAnUnspecifiedPeriod()
 {
  var dto=new ProposalComplianceInputDto("UNIT-TEST",EconomicAppraisal:new EconomicAppraisalDto(CashFlows:[-100m,110m]));
  Assert.Throws<InvalidOperationException>(()=>ProposalComplianceInputMapper.Map(dto,strictDemonstrationInput:true));
 }
 [Fact] public void FullCatalogueReturnsEveryRuleFindingWithoutForcingCompleteness()
 {
  var rules=NpdRuleCatalogue.GetDefaultRuleDefinitions();
  var findings=NpdRuleCatalogue.EvaluateAll(new ProposalComplianceInput("UNIT-TEST"),rules);
  Assert.Equal(24,findings.Count);
  Assert.Equal(rules.Select(r=>r.RuleCode).OrderBy(x=>x),findings.Select(f=>f.RuleCode).OrderBy(x=>x));
  Assert.Contains(findings,f=>f.Status==RuleResultStatus.InsufficientInformation);
 }
 [Theory][InlineData(null)][InlineData("invented")][InlineData("999")]
 public void MissingOrUnsupportedEvidenceVerificationDoesNotBecomeProvided(string? status)
 {
  var dto=new ProposalComplianceInputDto("UNIT-TEST",EvidenceReferences:[new EvidenceReferenceDto("UNIT-EVIDENCE","UNIT-REFERENCE",status)]);
  Assert.Equal(EvidenceStatus.Missing,ProposalComplianceInputMapper.Map(dto,strictDemonstrationInput:true).EvidenceReferences!.Single().VerificationStatus);
 }
 [Fact] public void ExplicitEmptyStakeholdersAndStructuredBudgetSurviveMapping()
 {
  var dto=new ProposalComplianceInputDto("UNIT-TEST",Stakeholders:new StakeholderDto(ExpectedStakeholders:[]),Budget:new BudgetDto(SubmittedProjectBudget:25m,CostComponents:[new CostComponentDto(Amount:25m)]));
  var mapped=ProposalComplianceInputMapper.Map(dto,strictDemonstrationInput:true);
  Assert.Empty(mapped.Stakeholders!.ExpectedStakeholders!);
  Assert.Equal(25m,mapped.Budget!.CostComponents!.Single().Amount);
  Assert.Null(mapped.ImpactAssessment);
 }
}
