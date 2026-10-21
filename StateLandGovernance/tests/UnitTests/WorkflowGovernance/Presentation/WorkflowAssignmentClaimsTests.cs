using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.WorkflowGovernance.Presentation.Security;
namespace StateLandGovernance.UnitTests.WorkflowGovernance.Presentation;
public sealed class WorkflowAssignmentClaimsTests
{
    private static readonly Guid Actor = Guid.NewGuid(), Case = Guid.NewGuid(), Document = Guid.NewGuid();
    private static ClaimsPrincipal Principal(Guid? actor = null) => new(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, (actor ?? Actor).ToString()),
        new Claim(ClaimTypes.Role, "Officer"),
        new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()) }, "verified-test-token"));
    private static IConfiguration Config(bool expired = false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["WorkflowGovernance:AuthorityAssignments:0:ActorId"] = Actor.ToString(),
        ["WorkflowGovernance:AuthorityAssignments:0:CaseId"] = Case.ToString(),
        ["WorkflowGovernance:AuthorityAssignments:0:DocumentId"] = Document.ToString(),
        ["WorkflowGovernance:AuthorityAssignments:0:SourceReference"] = "TEST-ONLY-reviewed-assignment",
        ["WorkflowGovernance:AuthorityAssignments:0:ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 10).ToString("O"),
        ["WorkflowGovernance:AuthorityAssignments:0:Capabilities:0"] = "HumanEvidenceReviewer"
    }).Build();
    [Fact] public async Task ExistingHostOfficerRoleMapsWithoutCapabilityGrant() {
        var p=Principal(); var id=(ClaimsIdentity)p.Identity!;
        id.RemoveClaim(id.FindFirst(ClaimTypes.Role)!); id.AddClaim(new Claim(ClaimTypes.Role,"land_intelligence_officer"));
        p=await new WorkflowAssignmentClaimsTransformation(new ConfigurationBuilder().Build()).TransformAsync(p);
        Assert.True(p.IsInRole("Officer")); Assert.False(p.HasClaim(c=>c.Type=="capability"));
    }
    [Fact] public async Task OfficerRoleAloneGrantsNoCapabilities() {
        var p = await new WorkflowAssignmentClaimsTransformation(new ConfigurationBuilder().Build()).TransformAsync(Principal());
        Assert.False(p.HasClaim(c => c.Type == "capability"));
    }
    [Fact] public async Task ExplicitAssignmentBindsActorCaseDocumentAndExpiry() {
        var transformer = new WorkflowAssignmentClaimsTransformation(Config()); var p = await transformer.TransformAsync(Principal());
        await transformer.TransformAsync(p);
        Assert.True(p.HasClaim("authority_case_id",Case.ToString())); Assert.True(p.HasClaim("authority_document_id",Document.ToString()));
        Assert.Single(p.FindAll("capability")); Assert.False(p.HasClaim("capability","WorkflowPlanApprover"));
        Assert.InRange(long.Parse(p.FindFirst("exp")!.Value),DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow.AddMinutes(11).ToUnixTimeSeconds());
    }
    [Fact] public async Task OtherActorAndExpiredAssignmentsDoNotGrant() {
        Assert.False((await new WorkflowAssignmentClaimsTransformation(Config()).TransformAsync(Principal(Guid.NewGuid()))).HasClaim(c=>c.Type=="capability"));
        Assert.False((await new WorkflowAssignmentClaimsTransformation(Config(true)).TransformAsync(Principal())).HasClaim(c=>c.Type=="capability"));
    }
    [Fact] public async Task AnonymousClaimsNeverEstablishAuthority() {
        var p = new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,Actor.ToString())}));
        Assert.False((await new WorkflowAssignmentClaimsTransformation(Config()).TransformAsync(p)).HasClaim(c=>c.Type=="capability"));
    }
}
