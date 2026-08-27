using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GovernanceIntelligenceControllerExplainTests
{
    private class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _time;
        public TestTimeProvider(DateTimeOffset time) => _time = time;
        public override DateTimeOffset GetUtcNow() => _time;
    }

    private class StubRegulatoryRuleProvider : IRegulatoryRuleProvider
    {
        public Task<IEnumerable<RegulatoryRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<RegulatoryRule> rules = new List<RegulatoryRule>();
            return Task.FromResult(rules);
        }
    }

    private class SpyAuditRepository : IGovernanceAuditRepository
    {
        public List<GovernanceAuditRecord> AddedRecords { get; } = new();

        public Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
        {
            AddedRecords.Add(record);
            return Task.CompletedTask;
        }

        public Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AddedRecords.FirstOrDefault(r => r.Id == id));
        }

        public Task<IReadOnlyList<GovernanceAuditRecord>> GetByEngineTypeAsync(EngineType engineType, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<GovernanceAuditRecord> list = AddedRecords.Where(r => r.EngineType == engineType).ToList();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<GovernanceAuditRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<GovernanceAuditRecord> list = AddedRecords.ToList();
            return Task.FromResult(list);
        }
    }

    private readonly SpyAuditRepository _auditRepo;
    private readonly StubRegulatoryRuleProvider _ruleProvider;
    private readonly TestTimeProvider _testTimeProvider;
    private readonly ExplainableGovernanceEngine _explanationEngine;
    private readonly GenerateGovernanceExplanationCommandHandler _explanationHandler;
    private readonly InMemoryGovernanceEvaluationStore _evalStore;

    public GovernanceIntelligenceControllerExplainTests()
    {
        _auditRepo = new SpyAuditRepository();
        _evalStore = new InMemoryGovernanceEvaluationStore(_auditRepo);
        _ruleProvider = new StubRegulatoryRuleProvider();
        _testTimeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        _explanationEngine = new ExplainableGovernanceEngine();

        _explanationHandler = new GenerateGovernanceExplanationCommandHandler(
            _explanationEngine,
            _evalStore,
            _testTimeProvider);
    }

    [Fact]
    public async Task Controller_Explain_ValidRequest_ShouldReturnOkResult()
    {
        var complianceEngine = new RegulatoryComplianceEngine();
        var complianceHandler = new EvaluateComplianceCommandHandler(_ruleProvider, complianceEngine, _evalStore);

        var conflictHandler = new DetectConflictsCommandHandler(
            new GovernanceConflictEngine(),
            _evalStore,
            _testTimeProvider);

        var riskHandler = new EvaluateGovernanceRiskCommandHandler(
            new GovernanceRiskEngine(),
            _evalStore,
            _testTimeProvider);

        var consensusHandler = new EvaluateGovernanceConsensusCommandHandler(
            new GovernanceConsensusEngine(),
            _evalStore,
            _testTimeProvider);

        var verificationHandler = new EvaluateConditionalVerificationCommandHandler(
            new ConditionalGovernanceVerificationEngine(),
            _evalStore,
            _testTimeProvider);

        var controller = new GovernanceIntelligenceController(
            complianceHandler,
            conflictHandler,
            riskHandler,
            _explanationHandler,
            consensusHandler,
            verificationHandler);

        var inputDto = new GovernanceExplanationInputDto("PARCEL-301", null, null, null);
        var command = new GenerateGovernanceExplanationCommand("ExplainAction", inputDto);

        var actionResult = await controller.Explain(command, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var resultDto = Assert.IsType<GovernanceExplanationResultDto>(okResult.Value);
        Assert.Equal("PARCEL-301", resultDto.SubjectId);
        Assert.Equal("Low", resultDto.OverallSeverity);
    }

    [Fact]
    public async Task Controller_Explain_InvalidRequest_ShouldReturnBadRequest()
    {
        var complianceEngine = new RegulatoryComplianceEngine();
        var complianceHandler = new EvaluateComplianceCommandHandler(_ruleProvider, complianceEngine, _evalStore);

        var conflictHandler = new DetectConflictsCommandHandler(
            new GovernanceConflictEngine(),
            _evalStore,
            _testTimeProvider);

        var riskHandler = new EvaluateGovernanceRiskCommandHandler(
            new GovernanceRiskEngine(),
            _evalStore,
            _testTimeProvider);

        var consensusHandler = new EvaluateGovernanceConsensusCommandHandler(
            new GovernanceConsensusEngine(),
            _evalStore,
            _testTimeProvider);

        var verificationHandler = new EvaluateConditionalVerificationCommandHandler(
            new ConditionalGovernanceVerificationEngine(),
            _evalStore,
            _testTimeProvider);

        var controller = new GovernanceIntelligenceController(
            complianceHandler,
            conflictHandler,
            riskHandler,
            _explanationHandler,
            consensusHandler,
            verificationHandler);

        var actionResultNullCommand = await controller.Explain(null!, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(actionResultNullCommand.Result);

        var inputDto = new GovernanceExplanationInputDto("", null, null, null);
        var commandInvalidInput = new GenerateGovernanceExplanationCommand("Action", inputDto);

        var actionResultInvalid = await controller.Explain(commandInvalidInput, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(actionResultInvalid.Result);
    }

    [Fact]
    public void DI_AddExplainableGovernanceEngine_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGovernanceAuditRepository>(_auditRepo);
        services.AddSingleton<IGovernanceEvaluationStore>(_evalStore);
        services.AddExplainableGovernanceEngine();

        var provider = services.BuildServiceProvider();

        var engine = provider.GetService<IExplainableGovernanceEngine>();
        Assert.NotNull(engine);
        Assert.IsType<ExplainableGovernanceEngine>(engine);

        var handler = provider.GetService<GenerateGovernanceExplanationCommandHandler>();
        Assert.NotNull(handler);
    }

    [Fact]
    public void ModuleBoundary_GovernanceIntelligence_ShouldNotDependOnPeerModules()
    {
        var assembly = typeof(ExplainableGovernanceEngine).Assembly;
        var referencedAssemblies = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, r => r.Name != null && r.Name.Contains("LandIntelligence"));
        Assert.DoesNotContain(referencedAssemblies, r => r.Name != null && r.Name.Contains("LeaseFeasibility"));
        Assert.DoesNotContain(referencedAssemblies, r => r.Name != null && r.Name.Contains("WorkflowGovernance"));
    }

    [Fact]
    public void Controller_Explain_ShouldContainNoExplanationBusinessRules()
    {
        var controllerType = typeof(GovernanceIntelligenceController);
        var fields = controllerType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert controller does not hold direct domain engine references for explanation synthesis
        Assert.DoesNotContain(fields, f => f.FieldType == typeof(ExplainableGovernanceEngine) || f.FieldType == typeof(IExplainableGovernanceEngine));

        // Assert controller depends on application command handler
        Assert.Contains(fields, f => f.FieldType == typeof(GenerateGovernanceExplanationCommandHandler));
    }
}
