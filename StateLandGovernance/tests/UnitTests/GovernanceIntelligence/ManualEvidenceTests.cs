using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class ManualEvidenceTests {
 const string Concern="LegalDispute";
 [Theory][InlineData("Verified")][InlineData("Rejected")][InlineData("MoreInformationRequired")]
 public void Decision_RequiresRemarksAndPreservesExplicitPurpose(string decision){var input=new VerifyScreeningEvidence(Guid.NewGuid(),Concern,0,decision,"Reviewed the specific supplied record for this concern.",[],["DS record123"]);Assert.Equal(decision,ManualEvidenceInput.Validate(input).Decision);Assert.Throws<ArgumentException>(()=>ManualEvidenceInput.Validate(input with{Remarks="ok"}));}
 [Fact]public void VerifiedRequiresEvidenceAndKnownConcern(){var input=new VerifyScreeningEvidence(Guid.NewGuid(),Concern,0,"Verified","Reviewed this specified supporting record carefully.",[],[]);Assert.Throws<ArgumentException>(()=>ManualEvidenceInput.Validate(input));Assert.Throws<ArgumentException>(()=>ManualEvidenceInput.Validate(input with{Concern="invented",EvidenceReferences=["reference"]}));}
 [Theory][InlineData("../secret.txt","text/plain","text")][InlineData("evidence.html","text/html","<script>bad</script>")][InlineData("fake.pdf","application/pdf","text")]
 public void AttachmentRejectsTraversalUnsupportedAndMismatchedContent(string name,string type,string body)=>Assert.Throws<ArgumentException>(()=>ManualEvidenceInput.Validate(new AttachScreeningEvidence(Guid.NewGuid(),Concern,name,type,System.Text.Encoding.UTF8.GetBytes(body))));
 [Fact]public void AttachmentIsBoundedAndAcceptsPlainText(){var input=new AttachScreeningEvidence(Guid.NewGuid(),Concern,"record.txt","text/plain","A specified fictional supporting record."u8.ToArray());Assert.Equal(input,ManualEvidenceInput.Validate(input));Assert.Throws<ArgumentException>(()=>ManualEvidenceInput.Validate(input with{Content=new byte[ManualEvidenceInput.MaxBytes+1]}));}
}
