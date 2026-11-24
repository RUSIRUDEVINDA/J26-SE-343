using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class C4FollowupCompletionTests {
 static readonly Guid Evaluation=Guid.NewGuid();
 static ComplianceFindingDto Finding(string status="InsufficientInformation",bool review=false)=>new("RULE","1","Test","Applicable",status,"Review",true,review,"Original input","Original outstanding requirement","Unverified","MissingInputs","Authority","Source","Section","Recommendation");
 static RegulatoryFollowupEvent Event(ComplianceFindingDto f,string operation="ClarificationRequested",string? outcome=null)=>new(Guid.NewGuid(),Evaluation,f.RuleCode,f.RuleVersion,Guid.NewGuid(),DateTimeOffset.UtcNow,operation,outcome,operation=="ClarificationRequested"?"The missing document is requested for downstream handling.":null,"The officer has documented the required screening follow-up.",[],"Recorded",1,null,new string('a',64));
 [Fact] public void CompliantAutomaticallyAddressed()=>Assert.Empty(RegulatoryProgression.Holds([Finding("Compliant")],[],Evaluation));
 [Theory][InlineData("InsufficientInformation")][InlineData("RequiresHumanReview")]
 public void MissingFollowupBlocks(string status)=>Assert.Contains("awaiting officer follow-up",Assert.Single(RegulatoryProgression.Holds([Finding(status)],[],Evaluation)));
 [Fact] public void ClarificationAddressesButPreservesOutstandingConcern(){var f=Finding();var e=Event(f);var original=JsonSerializer.Serialize(new{f,e});Assert.Empty(RegulatoryProgression.Holds([f],[e],Evaluation));Assert.Equal(original,JsonSerializer.Serialize(new{f,e}));}
 [Theory][InlineData("NoAdditionalConcern")][InlineData("AdditionalInformationRequired")][InlineData("FurtherInstitutionalLegalReview")]
 public void EveryValidReviewOutcomeAddresses(string outcome){var f=Finding("RequiresHumanReview",true);Assert.Empty(RegulatoryProgression.Holds([f],[Event(f,"HumanReviewRecorded",outcome)],Evaluation));}
 [Fact] public void EligibleInformationHumanReviewAddresses(){var f=Finding(review:true);Assert.Empty(RegulatoryProgression.Holds([f],[Event(f,"HumanReviewRecorded","AdditionalInformationRequired")],Evaluation));}
 [Fact] public void IneligibleInformationHumanReviewDoesNotAddress(){var f=Finding();Assert.Single(RegulatoryProgression.Holds([f],[Event(f,"HumanReviewRecorded","NoAdditionalConcern")],Evaluation));}
 [Fact] public void InvalidOrForeignEventsDoNotAddress(){var f=Finding();var e=Event(f);foreach(var invalid in new[]{e with{EvaluationId=Guid.NewGuid()},e with{RuleCode="OTHER"},e with{RuleVersion="2"},e with{Status="Pending"},e with{Remarks="short"},e with{Revision=0},e with{MissingInformation="short"},e with{ReviewOutcome="NoAdditionalConcern"}})Assert.Single(RegulatoryProgression.Holds([f],[invalid],Evaluation));}
 [Fact] public void LatestValidApplicableEventDeterminesCompletion(){var f=Finding(review:true);var first=Event(f);var second=Event(f,"HumanReviewRecorded","FurtherInstitutionalLegalReview") with{Revision=2,PreviousEventId=first.Id};Assert.Empty(RegulatoryProgression.Holds([f],[first,second,second with{Revision=3,Remarks="bad"}],Evaluation));}
 [Fact] public void FourteenCompliantSevenClarificationsThreeReviewsCompleteAllTwentyFour(){var findings=Enumerable.Range(0,24).Select(i=>Finding(i<14?"Compliant":i<21?"InsufficientInformation":"RequiresHumanReview",i>=21) with{RuleCode="RULE-"+i}).ToArray();var clarifications=findings.Skip(14).Take(7).Select(f=>Event(f)).ToArray();Assert.Equal(3,RegulatoryProgression.Holds(findings,clarifications,Evaluation).Length);var events=clarifications.Concat(findings.Skip(21).Select((f,i)=>Event(f,"HumanReviewRecorded",RegulatoryFollowupInput.Outcomes[i]))).ToArray();Assert.Single(RegulatoryProgression.Holds(findings,events[..^1],Evaluation));Assert.Empty(RegulatoryProgression.Holds(findings,events,Evaluation));}
 [Fact] public void EmptyAndDuplicateRuleFindingsStillFailClosed(){Assert.Single(RegulatoryProgression.Holds([],[],Evaluation));Assert.Single(RegulatoryProgression.Holds([Finding(),Finding()],[],Evaluation));}
}
