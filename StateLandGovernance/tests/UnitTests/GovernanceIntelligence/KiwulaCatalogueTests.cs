using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using Xunit;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class KiwulaCatalogueTests
{
    [Fact] public void EvidenceGapsNeverBecomeVerifiedAbsence()
    {
        Assert.Equal(7,KiwulaCatalogue.Indicators.Count);
        Assert.All(KiwulaCatalogue.Indicators,x=>{Assert.Equal("Missing",x.EvidenceState);Assert.Null(x.EvidenceReference);});
        Assert.All(KiwulaCatalogue.Decisions,x=>Assert.Contains("Example",x.InstitutionName));
    }
    [Fact] public void NewExampleHasSeparateVersionAndNoOfficialNotificationDocument()
    {
        var p=PostWorkflowDemonstrations.Create(KiwulaCatalogue.WorkflowScenario,Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow);
        Assert.Equal("DS Final Notification",p.Events.Last().Activity); Assert.Equal(7,p.Events.Count);
        Assert.Equal("kiwula-completed-example/1",PostWorkflowDemonstrations.VersionFor(KiwulaCatalogue.WorkflowScenario));
        Assert.Equal("c4-fictional-workflow-v1",PostWorkflowDemonstrations.VersionFor("administrative"));
        Assert.All(p.Events,x=>Assert.StartsWith("fictional://",x.EvidenceRef));
        Assert.Equal(Enumerable.Range(1,7),p.Events.Select(x=>x.EventSeq));
    }
    [Theory] [InlineData("Production",true)] [InlineData("Development",false)]
    public async Task InactiveOrNonDevelopmentCatalogueDoesNotTouchStorage(string environment,bool enabled)
    {
        var c=new KiwulaAssessmentsController(null!,null!,new Env{EnvironmentName=environment},new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"GovernanceIntelligence:KiwulaWorkspace:Enabled",enabled.ToString()}}).Build()) {ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        c.HttpContext.User=new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,"Officer")],"unit"));
        Assert.Equal(503,Assert.IsType<ObjectResult>(await c.Catalogue(default)).StatusCode);
    }
    [Theory] [InlineData(false,"Officer",401)] [InlineData(true,"Applicant",403)]
    public async Task MissingAuthenticationOrOfficerRoleDeniesBeforeStorage(bool authenticated,string role,int status)
    {
        var c=new KiwulaAssessmentsController(null!,null!,new Env(),new ConfigurationBuilder().Build()){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        c.HttpContext.User=new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,role)],authenticated?"unit":null));
        var response=await c.Catalogue(default);Assert.True(status==401?response is UnauthorizedResult:response is ForbidResult);
    }
    sealed class Env:IHostEnvironment {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="unit";public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
