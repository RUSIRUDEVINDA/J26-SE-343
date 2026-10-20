using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using StateLandGovernance.Api.Security;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.LandIntelligence.Presentation.Security;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using Xunit;
public sealed class UploadedCaseResearchScenarioTests
{
    [Theory][InlineData("Production",true,"127.0.0.1")][InlineData("Development",false,"127.0.0.1")][InlineData("Development",true,"192.0.2.5")]
    public async Task DesignationIsUnavailableOutsideExplicitLocalDevelopment(string environment,bool enabled,string ip)
    { var f=new Fixture(environment,enabled,ip);Assert.False(await f.Service.AssignedAsync(f.Case));Assert.Null(await f.Service.ReadAsync(f.Case)); }
    [Fact] public async Task OfficerRoleCannotReplaceDurableDocumentAssignment()
    {var f=new Fixture();Assert.True(await f.Service.AssignedAsync(f.Case,f.Doc));Assert.False(await f.Service.AssignedAsync(Guid.NewGuid()));Assert.False(await f.Service.AssignedAsync(f.Case,Guid.NewGuid()));f.Assignments.State=f.Assignments.State with {Active=null};Assert.False(await f.Service.AssignedAsync(f.Case));}
    [Fact] public async Task DesignationRequiresExplicitRecordAndCurrentDocumentVersionAndRevocationRemovesUse()
    {
        var f=new Fixture();Assert.False(await f.Service.CanUseAsync(f.Case));
        await f.Service.DesignateAsync(f.Case,f.Doc,f.Version,"Explicitly synthetic test source reviewed",default);
        Assert.True(await f.Service.CanUseAsync(f.Case));Assert.Single(f.Records.Items);Assert.Equal("DevelopmentSyntheticDesignation",f.Records.Items[0].Purpose);
        f.Records.Items[0].PayloadJson=JsonSerializer.Serialize(new UploadedCaseResearchScenarios.Designation(f.Doc,Guid.NewGuid(),"old version"));Assert.False(await f.Service.CanUseAsync(f.Case));
        f.Assignments.State=f.Assignments.State with {Active=null};Assert.False(await f.Service.CanUseAsync(f.Case));
    }
    private sealed class Fixture
    {
        public Guid Case=Guid.NewGuid(),Doc=Guid.NewGuid(),Version=Guid.NewGuid(),Actor=Guid.NewGuid();
        public Assignments Assignments;public Records Records=new();public UploadedCaseResearchScenarios Service;
        public Fixture(string env="Development",bool enabled=true,string ip="127.0.0.1")
        {
            var environment=new Env(env);var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["LocalDevelopmentSignIn:Enabled"]=enabled.ToString()}).Build();
            var accounts=new DevelopmentAccounts(cfg,environment,new LandIntelligenceJwtTokenService(Options.Create(new LandIntelligenceAuthenticationOptions{JwtSigningKeyBase64=Convert.ToBase64String(new byte[32])})));
            var http=new DefaultHttpContext();http.Connection.RemoteIpAddress=System.Net.IPAddress.Parse(ip);http.User=new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,Actor.ToString()),new(ClaimTypes.Role,"Officer")],"test"));
            Assignments=new(new(Case,1,null,"TEST",null,null,"TEST",new(Guid.NewGuid(),Actor,"TEST",Doc,["CaseWorkspaceReviewer","FactVerifier"],Actor,"test",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1)),[]));
            var now=DateTime.UtcNow;var authority=new VerifiedAuthoritySnapshot(Actor,["DocumentSubmitter"],new(AuthorityScopeKind.LeaseCase,Case.ToString()),now.AddMinutes(-1),now,now.AddHours(1));
            var document=new GovernedDocument(new(Doc),new(Case),"Proposal",new(Version),new("SHA-256",new string('a',64)),new("test/source"),"arbitrary.pdf","application/pdf",100,Actor,now,authority);
            Service=new(accounts,environment,new HttpContextAccessor{HttpContext=http},Assignments,Records,new Docs(document),new Unit());
        }
    }
    public sealed class Assignments(CaseAssignmentState state):ICaseAssignmentStore
    {
        public CaseAssignmentState State=state;
        public Task<CaseAssignmentState?> ReadAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<CaseAssignmentState?>(id==State.CaseId?State:null);
        public Task<CaseAssignmentState> EnsureAsync(Guid id,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<IReadOnlyList<IntakeCaseSummary>> ListAsync(Guid? a,Guid? r,string? o,bool u,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<CaseAssignmentState> SaveAsync(CaseAssignmentState s,int r,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<bool> DocumentBelongsAsync(Guid c,Guid d,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<IReadOnlyList<IntakeJurisdictionPolicy>> PoliciesAsync(CancellationToken ct=default)=>throw new NotSupportedException();
        public Task PublishPolicyAsync(IntakeJurisdictionPolicy p,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    public sealed class Records:ICorrectionRepository
    {
        public List<CorrectionRecord> Items=[];
        public Task<CorrectionRecord?> GetAsync(Guid c,Guid id,CancellationToken ct=default)=>Task.FromResult(Items.FirstOrDefault(x=>x.LeaseCaseId==c&&x.Id==id));
        public Task<IReadOnlyList<CorrectionRecord>> ListAsync(Guid c,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<CorrectionRecord>>(Items.Where(x=>x.LeaseCaseId==c).ToArray());
        public Task SaveAsync(CorrectionRecord r,CancellationToken ct=default){Items.Add(r);return Task.CompletedTask;}
    }
    private sealed class Docs(GovernedDocument d):IGovernedDocumentRepository
    {public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id,CancellationToken ct=default)=>Task.FromResult<GovernedDocument?>(id==d.Id?d:null);public Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId id,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<GovernedDocument>>([d]);public Task AddAsync(GovernedDocument d,CancellationToken ct=default)=>throw new NotSupportedException();}
    private sealed class Unit:IWorkflowGovernanceUnitOfWork{public Task<int> CommitAsync(CancellationToken ct=default)=>Task.FromResult(1);}
    private sealed class Env(string name):IHostEnvironment{public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="test";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
