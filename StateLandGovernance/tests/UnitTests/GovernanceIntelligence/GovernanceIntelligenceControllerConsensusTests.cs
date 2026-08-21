using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

public class GovernanceIntelligenceControllerConsensusTests
{
    private readonly IGovernanceAuditRepository _auditRepo;
    private readonly TimeProvider _timeProvider;
    private readonly EvaluateGovernanceConsensusCommandHandler _handler;
    private readonly GovernanceIntelligenceController _controller;

    private class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    }

    public GovernanceIntelligenceControllerConsensusTests()
    {
        _auditRepo = new InMemoryGovernanceAuditRepository();
        _timeProvider = new FixedTimeProvider();
        
        var ruleProvider = new InMemoryRegulatoryRuleProvider();
        var complianceEngine = new RegulatoryComplianceEngine();
        var conflictEngine = new GovernanceConflictEngine();
        var riskEngine = new GovernanceRiskEngine();
        var explanationEngine = new ExplainableGovernanceEngine();
        var consensusEngine = new GovernanceConsensusEngine();
        var evalStore = new InMemoryGovernanceEvaluationStore(_auditRepo);

        var complianceHandler = new EvaluateComplianceCommandHandler(ruleProvider, complianceEngine, evalStore);
        var conflictHandler = new DetectConflictsCommandHandler(conflictEngine, evalStore, _timeProvider);
        var riskHandler = new EvaluateGovernanceRiskCommandHandler(riskEngine, evalStore, _timeProvider);
        var explanationHandler = new GenerateGovernanceExplanationCommandHandler(explanationEngine, evalStore, _timeProvider);
        _handler = new EvaluateGovernanceConsensusCommandHandler(consensusEngine, evalStore, _timeProvider);
        var verificationEngine = new ConditionalGovernanceVerificationEngine();
        var verificationHandler = new EvaluateConditionalVerificationCommandHandler(verificationEngine, evalStore, _timeProvider);

        _controller = new GovernanceIntelligenceController(complianceHandler, conflictHandler, riskHandler, explanationHandler, _handler, verificationHandler);
    }

    [Fact]
    public async Task Controller_EvaluateConsensus_ValidRequest_ShouldReturnOkResult()
    {
        var inputDto = new GovernanceConsensusInputDto(
            SubjectId: "PARCEL-CTRL-1",
            Positions: new List<InstitutionalGovernancePositionDto> { new("INST-A", "Approve") },
            Policy: new GovernanceConsensusPolicyDto("SimpleMajority", new List<string> { "INST-A" })
        );

        var command = new EvaluateGovernanceConsensusCommand("Action_CtrlConsensus", inputDto);

        var actionResult = await _controller.EvaluateConsensus(command, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<GovernanceConsensusResultDto>(okResult.Value);
        Assert.Equal("ConsensusReached", dto.Outcome);
    }

    [Fact]
    public async Task Controller_EvaluateConsensus_InvalidRequest_ShouldReturnBadRequest()
    {
        var actionResultNull = await _controller.EvaluateConsensus(null!, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(actionResultNull.Result);

        var inputDtoInvalid = new GovernanceConsensusInputDto(
            SubjectId: "PARCEL-CTRL-BAD",
            Positions: new List<InstitutionalGovernancePositionDto>(),
            Policy: new GovernanceConsensusPolicyDto("InvalidModeName", new List<string> { "INST-A" })
        );

        var commandInvalid = new EvaluateGovernanceConsensusCommand("Action_CtrlBad", inputDtoInvalid);
        var actionResultBad = await _controller.EvaluateConsensus(commandInvalid, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResultBad.Result);
    }

    [Fact]
    public void DI_AddGovernanceConsensusEngine_ShouldRegisterServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGovernanceAuditRepository>(_auditRepo);
        services.AddSingleton<IGovernanceEvaluationStore>(new InMemoryGovernanceEvaluationStore(_auditRepo));
        services.AddGovernanceConsensusEngine();

        var provider = services.BuildServiceProvider();

        var engine = provider.GetService<IGovernanceConsensusEngine>();
        Assert.NotNull(engine);
        Assert.IsType<GovernanceConsensusEngine>(engine);

        var handler = provider.GetService<EvaluateGovernanceConsensusCommandHandler>();
        Assert.NotNull(handler);
    }

    [Fact]
    public void Controller_EvaluateConsensus_ShouldContainNoBusinessRules()
    {
        var controllerType = typeof(GovernanceIntelligenceController);
        var fields = controllerType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.DoesNotContain(fields, f => f.FieldType == typeof(GovernanceConsensusEngine) || f.FieldType == typeof(IGovernanceConsensusEngine));
        Assert.Contains(fields, f => f.FieldType == typeof(EvaluateGovernanceConsensusCommandHandler));
    }

    [Fact]
    public void ModuleBoundary_GovernanceIntelligence_ShouldNotDependOnPeerModules()
    {
        var assembly = typeof(GovernanceConsensusEngine).Assembly;
        var referencedAssemblies = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, r => r.Name != null && r.Name.Contains("LandIntelligence"));
        Assert.DoesNotContain(referencedAssemblies, r => r.Name != null && r.Name.Contains("LeaseFeasibility"));
        Assert.DoesNotContain(referencedAssemblies, r => r.Name != null && r.Name.Contains("WorkflowGovernance"));
    }
}
