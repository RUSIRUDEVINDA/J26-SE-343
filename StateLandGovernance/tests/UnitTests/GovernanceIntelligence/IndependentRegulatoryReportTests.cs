using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public class IndependentRegulatoryReportTests
{
    sealed class Grants : IIndependentRegulatoryReadAssignments
    {
        public Guid Actor = Guid.NewGuid(); public List<Guid> Ids = new();
        public Task<IReadOnlyList<Guid>> ReadAllowedIdsAsync(Guid actor, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Guid>>(actor == Actor ? Ids.ToArray() : Array.Empty<Guid>());
    }
    sealed class Store : IIndependentRegulatoryReportStore
    {
        public int Reads; public Action? AfterRead; public IndependentRegulatoryReport? Report;
        public Task<IndependentRegulatoryReport?> GetAsync(Guid id, CancellationToken ct) { Reads++; AfterRead?.Invoke(); return Task.FromResult(Report); }
        public Task<IReadOnlyList<IndependentRegulatorySummary>> ListAsync(IReadOnlyList<Guid> ids, int limit, CancellationToken ct)
        { Reads++; AfterRead?.Invoke(); return Task.FromResult<IReadOnlyList<IndependentRegulatorySummary>>(Report == null ? Array.Empty<IndependentRegulatorySummary>() : new[]{Report.Metadata}); }
    }
    [Fact] public async Task MissingOrDifferentActorGrant_DeniesBeforeReadingAnyStoredData()
    {
        var g=new Grants();var s=new Store();var q=new IndependentRegulatoryReportQueries(s,g);var id=Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>q.GetAsync(g.Actor,id,default));
        g.Ids.Add(id);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>q.GetAsync(Guid.NewGuid(),id,default));
        Assert.Empty(await q.ListAsync(Guid.NewGuid(),30,default));Assert.Equal(0,s.Reads);
    }
    [Fact] public async Task ExactGrant_DoesNotAuthorizeOtherReport_AndRevocationIsRechecked()
    {
        var id=Guid.NewGuid();var g=new Grants{Ids=new(){id}};var s=new Store();var q=new IndependentRegulatoryReportQueries(s,g);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>q.GetAsync(g.Actor,Guid.NewGuid(),default));
        Assert.Equal(0,s.Reads);s.AfterRead=()=>g.Ids.Clear();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>q.GetAsync(g.Actor,id,default));
        Assert.Equal(1,s.Reads);
    }
    [Fact] public async Task ListRevokedDuringRead_ReturnsNoRows()
    {
        var id=Guid.NewGuid();var g=new Grants{Ids=new(){id}};
        var s=new Store{Report=new(new(id,Guid.NewGuid(),"group","proposal","action","InsufficientInformation",DateTime.UtcNow,"1.0.0",null),null)};
        s.AfterRead=()=>g.Ids.Clear();Assert.Empty(await new IndependentRegulatoryReportQueries(s,g).ListAsync(g.Actor,30,default));
    }
    [Fact] public async Task StoreReadsActualSavedJsonWithoutReevaluationOrTracking()
    {
        using var db=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var id=Guid.NewGuid();db.ComplianceEvaluations.Add(new ComplianceEvaluationEntity{Id=id,AuditRecordId=Guid.NewGuid(),CaseId="independent-label",ProposalId="proposal-label",Status="InsufficientInformation",RuleSetVersion="1.0.0",EvaluationTimestamp=DateTime.UtcNow,
          FindingsJson="""[{"RuleCode":"NPD-LOC-001","RuleVersion":"1.0","Category":"Completeness","Applicability":"Applicable","Status":"InsufficientInformation","Severity":"Medium","IsBlocking":false,"RequiresHumanReview":true,"ObservedValueSummary":"Province not supplied","ExpectedRequirement":"Location hierarchy","EvidenceStatus":"Missing","CalculationStatus":"MissingInputs","SourceAuthority":"Returned authority","SourceDocument":"Returned source","SourceSection":"Returned section","RecommendedAction":"Provide province"}]"""});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();var store=new IndependentRegulatoryReportStore(db);var result=await store.GetAsync(id,default);
        Assert.Equal("InsufficientInformation",result!.Metadata.Status);Assert.Equal(id,result.Metadata.Id);Assert.Equal("Province not supplied",Assert.Single(result.Findings!).ObservedValueSummary);
        Assert.Equal("Returned section",result.Findings![0].SourceSection);Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty(await store.ListAsync(new[]{Guid.NewGuid()},30,default));Assert.Single(await store.ListAsync(new[]{id},30,default));
    }
    [Fact] public async Task MissingFindingsRemainUnavailable_AndCorruptJsonIsNotInvented()
    {
        using var db=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var id=Guid.NewGuid();var row=new ComplianceEvaluationEntity{Id=id,FindingsJson=null};db.ComplianceEvaluations.Add(row);await db.SaveChangesAsync();
        var store=new IndependentRegulatoryReportStore(db);Assert.Null((await store.GetAsync(id,default))!.Findings);
        row.FindingsJson="broken";await db.SaveChangesAsync();await Assert.ThrowsAsync<IndependentRegulatoryReportUnavailableException>(()=>store.GetAsync(id,default));
    }
    [Fact] public async Task ControllerRequiresAuthenticationAndOfficerRole_GrantStillRequired()
    {
        var g=new Grants();var s=new Store();var c=new IndependentRegulatoryReportsController(new(s,g)){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        Assert.IsType<UnauthorizedResult>(await c.Get(Guid.NewGuid()));
        c.HttpContext.User=Principal(g.Actor,"Applicant");Assert.IsType<ForbidResult>(await c.Get(Guid.NewGuid()));
        c.HttpContext.User=Principal(g.Actor,"Officer");Assert.IsType<ForbidResult>(await c.Get(Guid.NewGuid()));Assert.Equal(0,s.Reads);
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(IndependentRegulatoryReportsController),typeof(AuthorizeAttribute)));
        var id=Guid.NewGuid();g.Ids.Add(id);Assert.IsType<NotFoundResult>(await c.Get(id));
    }
    [Fact] public async Task ControllerReturnsServiceFailureWithoutStoredPayload()
    {
        var g=new Grants();var id=Guid.NewGuid();g.Ids.Add(id);var c=new IndependentRegulatoryReportsController(new(new UnavailableIndependentRegulatoryReportStore(),g)){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        c.HttpContext.User=Principal(g.Actor,"Officer");var result=Assert.IsType<ObjectResult>(await c.Get(id));Assert.Equal(503,result.StatusCode);
    }
    static ClaimsPrincipal Principal(Guid actor,string role)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,role)},"unit"));
}
