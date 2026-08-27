using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GovernanceIntelligenceControllerConditionalVerificationTests
{
    private class DummyAuditRepo : IGovernanceAuditRepository
    {
        public Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<GovernanceAuditRecord?>(null);
    }

    private class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly GovernanceIntelligenceController _controller;

    public GovernanceIntelligenceControllerConditionalVerificationTests()
    {
        var auditRepo = new DummyAuditRepo();
        var evalStore = new InMemoryGovernanceEvaluationStore(auditRepo);
        var timeProvider = new FixedTimeProvider();
        var ruleProvider = new InMemoryRegulatoryRuleProvider();

        var complianceHandler = new EvaluateComplianceCommandHandler(ruleProvider, new RegulatoryComplianceEngine(), evalStore);
        var conflictHandler = new DetectConflictsCommandHandler(new GovernanceConflictEngine(), evalStore, timeProvider);
        var riskHandler = new EvaluateGovernanceRiskCommandHandler(new GovernanceRiskEngine(), evalStore, timeProvider);
        var explanationHandler = new GenerateGovernanceExplanationCommandHandler(new ExplainableGovernanceEngine(), evalStore, timeProvider);
        var consensusHandler = new EvaluateGovernanceConsensusCommandHandler(new GovernanceConsensusEngine(), evalStore, timeProvider);
        var verificationHandler = new EvaluateConditionalVerificationCommandHandler(new ConditionalGovernanceVerificationEngine(), evalStore, timeProvider);

        _controller = new GovernanceIntelligenceController(
            complianceHandler,
            conflictHandler,
            riskHandler,
            explanationHandler,
            consensusHandler,
            verificationHandler);
    }

    [Fact]
    public async Task VerifyConditions_ValidCommand_ReturnsOkResult()
    {
        var input = new ConditionalVerificationInputDto(
            SubjectId: "PARCEL-CTRL-VERIF-1",
            Conditions: new List<GovernanceConditionDto> { new("COND-1", "Condition 1") },
            Evidence: new List<VerificationEvidenceDto> { new("COND-1", ProvidedStatus: "Satisfied") }
        );

        var command = new EvaluateConditionalVerificationCommand("VerifyActionCtrl", input);

        var actionResult = await _controller.VerifyConditions(command, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<ConditionalVerificationResultDto>(okResult.Value);
        Assert.Equal("PARCEL-CTRL-VERIF-1", dto.SubjectId);
        Assert.Equal("FullySatisfied", dto.Outcome);
    }

    [Fact]
    public async Task VerifyConditions_InvalidCommand_ReturnsBadRequest()
    {
        var actionResultNull = await _controller.VerifyConditions(null!, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(actionResultNull.Result);

        var inputInvalid = new ConditionalVerificationInputDto(
            SubjectId: "  ",
            Conditions: new List<GovernanceConditionDto>()
        );

        var commandInvalid = new EvaluateConditionalVerificationCommand("VerifyBad", inputInvalid);
        var actionResultBad = await _controller.VerifyConditions(commandInvalid, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResultBad.Result);
    }

    [Fact]
    public void VerifyConditions_ThinController_DelegatesDirectlyToHandler()
    {
        var controllerType = typeof(GovernanceIntelligenceController);
        var fields = controllerType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.DoesNotContain(fields, f => f.FieldType == typeof(ConditionalGovernanceVerificationEngine) || f.FieldType == typeof(IConditionalGovernanceVerificationEngine));
        Assert.Contains(fields, f => f.FieldType == typeof(EvaluateConditionalVerificationCommandHandler));
    }
}
