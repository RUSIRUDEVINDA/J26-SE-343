using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class OfficerSequenceTests {
 static ComplianceFindingDto Finding()=>new("RULE","1","Test","Applicable","Compliant","Info",false,false,"Observed","Requirement","Provided","NotApplicable","Source","Document","1","None");
 static RegulatoryFollowupEvent Event(int revision=1,string outcome="NoAdditionalConcern")=>new(Guid.NewGuid(),Guid.NewGuid(),"RULE","1",Guid.NewGuid(),DateTimeOffset.UtcNow,"HumanReviewRecorded",outcome,null,"Reasoned review based on supplied records.",[],"Recorded",revision,null,new string('a',64));
 [Fact] public void CompliantSavedFinding_NoMandatoryReview_Progresses()=>Assert.Empty(RegulatoryProgression.Holds([Finding()],[]));
 [Fact] public void ExplicitNotApplicableFinding_WithoutBlockingOrReview_Progresses()=>Assert.Empty(RegulatoryProgression.Holds([Finding() with{Status="NotApplicable",Applicability="NotApplicable"}],[]));
 [Theory][InlineData("Applicable")][InlineData("Undetermined")]
 public void InconsistentNotApplicableFinding_RemainsHeld(string applicability)=>Assert.NotEmpty(RegulatoryProgression.Holds([Finding() with{Status="NotApplicable",Applicability=applicability}],[]));
 [Fact] public void NotApplicableFinding_DoesNotBypassBlockingOrRequiredReview(){var f=Finding() with{Status="NotApplicable",Applicability="NotApplicable"};Assert.NotEmpty(RegulatoryProgression.Holds([f with{IsBlocking=true}],[]));Assert.NotEmpty(RegulatoryProgression.Holds([f with{RequiresHumanReview=true}],[]));Assert.Empty(RegulatoryProgression.Holds([f with{RequiresHumanReview=true}],[Event()]));}
 [Fact] public void RequiredHumanReview_NeedsLatestMatchingValidFollowup(){var f=Finding() with{Status="RequiresHumanReview",RequiresHumanReview=true};Assert.NotEmpty(RegulatoryProgression.Holds([f],[]));Assert.Empty(RegulatoryProgression.Holds([f],[Event()]));Assert.Empty(RegulatoryProgression.Holds([f],[Event(),Event(2,"AdditionalInformationRequired")]));Assert.Empty(RegulatoryProgression.Holds([f],[Event(1,"FurtherInstitutionalLegalReview")]));Assert.NotEmpty(RegulatoryProgression.Holds([f],[Event() with{RuleVersion="other"}]));}
 [Theory][InlineData("InsufficientInformation")][InlineData("Conditional")][InlineData("NonCompliant")][InlineData("Unknown")]
 public void UnresolvedSavedOutcome_IsNotChangedByManualNote(string status)=>Assert.NotEmpty(RegulatoryProgression.Holds([Finding() with{Status=status}],[Event()]));
 [Fact] public void CompliantFindingIsSatisfiedForC4RegardlessOfOriginalFlag()=>Assert.Empty(RegulatoryProgression.Holds([Finding() with{IsBlocking=true}],[Event()]));
 [Fact] public void MissingOrAmbiguousFindings_AreHeld(){Assert.NotEmpty(RegulatoryProgression.Holds(null,[]));Assert.NotEmpty(RegulatoryProgression.Holds([],[]));Assert.NotEmpty(RegulatoryProgression.Holds([Finding(),Finding()],[]));}
 [Fact] public void LegacyCommandHashShape_PreservedWhenLinkAbsent(){var command=new EvaluateComplianceCommand("saved legacy",SubmissionKey:Guid.NewGuid());var original=JsonSerializer.Serialize(command with{SubmissionKey=null},RegulatoryJson.Options);Assert.DoesNotContain("earlyRequestId",original);var linked=JsonSerializer.Serialize(command with{EarlyRequestId=Guid.NewGuid(),SubmissionKey=null},RegulatoryJson.Options);Assert.Contains("earlyRequestId",linked);Assert.NotEqual(RegulatoryJson.Hash(original),RegulatoryJson.Hash(linked));}
 [Fact] public void LegacyProvenanceJson_RemainsReadableAndOmitsAbsentLink(){var p=new SavedRegulatoryProvenance(Guid.NewGuid(),Guid.NewGuid(),"hash","{}","input","1","schema","source","catalogue","1",null);var json=JsonSerializer.Serialize(p,RegulatoryJson.Options);Assert.DoesNotContain("earlyRequestId",json);Assert.Null(JsonSerializer.Deserialize<SavedRegulatoryProvenance>(json,RegulatoryJson.Options)!.EarlyRequestId);}
}
