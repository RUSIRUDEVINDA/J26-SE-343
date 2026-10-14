using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public class EvaluateGovernanceConsensusCommandHandlerTests
{
    private readonly IGovernanceConsensusEngine _engine;
    private readonly SpyAuditRepository _spyAuditRepo;
    private readonly FixedTimeProvider _timeProvider;
    private readonly EvaluateGovernanceConsensusCommandHandler _handler;

    private class SpyAuditRepository : IGovernanceAuditRepository
    {
        public GovernanceAuditRecord? CapturedRecord { get; private set; }
        public CancellationToken CapturedToken { get; private set; }

        public Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GovernanceAuditRecord?>(null);
        }

        public Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
        {
            CapturedRecord = record;
            CapturedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    }

    public EvaluateGovernanceConsensusCommandHandlerTests()
    {
        _engine = new GovernanceConsensusEngine();
        _spyAuditRepo = new SpyAuditRepository();
        var evalStore = new InMemoryGovernanceEvaluationStore(_spyAuditRepo);
        _timeProvider = new FixedTimeProvider();
        _handler = new EvaluateGovernanceConsensusCommandHandler(_engine, evalStore, _timeProvider);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ShouldCreateAuditRecord()
    {
        var inputDto = new GovernanceConsensusInputDto(
            SubjectId: "PARCEL-AUDIT-101",
            Positions: new List<InstitutionalGovernancePositionDto>
            {
                new("INST-A", "Approve"),
                new("INST-B", "Approve")
            },
            Policy: new GovernanceConsensusPolicyDto(
                Mode: "SimpleMajority",
                ExpectedInstitutionIds: new List<string> { "INST-A", "INST-B" }
            )
        );

        var command = new EvaluateGovernanceConsensusCommand("Action_EvaluateConsensus", inputDto);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("ConsensusReached", result.Outcome);

        Assert.NotNull(_spyAuditRepo.CapturedRecord);
        Assert.Equal(EngineType.Consensus, _spyAuditRepo.CapturedRecord.EngineType);
        Assert.Equal("Action_EvaluateConsensus", _spyAuditRepo.CapturedRecord.ActionName);
        Assert.Equal("ConsensusReached", _spyAuditRepo.CapturedRecord.Status);
    }

    [Fact]
    public async Task HandleAsync_AuditRecord_ShouldContainOnlySafeAggregateMetadata()
    {
        var inputDto = new GovernanceConsensusInputDto(
            SubjectId: "SECRET-SUBJECT-SENSITIVE-PII",
            Positions: new List<InstitutionalGovernancePositionDto>
            {
                new("INST-A", "Approve", SummaryNotes: "CONFIDENTIAL_PII_COMMENT_DO_NOT_STORE")
            },
            Policy: new GovernanceConsensusPolicyDto(
                Mode: "SimpleMajority",
                ExpectedInstitutionIds: new List<string> { "INST-A" }
            )
        );

        var command = new EvaluateGovernanceConsensusCommand("Action_AuditPrivacy", inputDto);

        await _handler.HandleAsync(command, CancellationToken.None);

        var details = _spyAuditRepo.CapturedRecord!.Details;

        Assert.DoesNotContain("SECRET-SUBJECT-SENSITIVE-PII", details);
        Assert.DoesNotContain("CONFIDENTIAL_PII_COMMENT_DO_NOT_STORE", details);
        Assert.Contains("Evaluated consensus across 1 expected institutions", details);
        Assert.Contains("Approvals: 1", details);
    }

    [Fact]
    public async Task HandleAsync_CancellationTokenPassed_ShouldPropagateToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var inputDto = new GovernanceConsensusInputDto(
            SubjectId: "PARCEL-CANCEL-1",
            Positions: new List<InstitutionalGovernancePositionDto> { new("INST-A", "Approve") },
            Policy: new GovernanceConsensusPolicyDto("SimpleMajority", new List<string> { "INST-A" })
        );

        var command = new EvaluateGovernanceConsensusCommand("Action_CancelToken", inputDto);

        await _handler.HandleAsync(command, token);

        Assert.Equal(token, _spyAuditRepo.CapturedToken);
    }

    [Fact]
    public async Task HandleAsync_NullCommand_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.HandleAsync(null!));
    }
}
