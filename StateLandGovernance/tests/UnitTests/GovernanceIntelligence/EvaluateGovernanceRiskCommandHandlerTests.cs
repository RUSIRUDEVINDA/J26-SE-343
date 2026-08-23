using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class EvaluateGovernanceRiskCommandHandlerTests
{
    private class TestTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _time;
        public TestTimeProvider(DateTimeOffset time) => _time = time;
        public override DateTimeOffset GetUtcNow() => _time;
    }

    private class SpyAuditRepository : IGovernanceAuditRepository
    {
        public List<GovernanceAuditRecord> AddedRecords { get; } = new();
        public CancellationToken CapturedToken { get; private set; }

        public Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GovernanceAuditRecord?>(null);
        }

        public Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
        {
            AddedRecords.Add(record);
            CapturedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private readonly GovernanceRiskEngine _engine = new();
    private readonly SpyAuditRepository _auditRepository = new();
    private readonly TestTimeProvider _timeProvider;
    private readonly EvaluateGovernanceRiskCommandHandler _handler;
    private readonly DateTimeOffset _fixedTime = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    public EvaluateGovernanceRiskCommandHandlerTests()
    {
        _timeProvider = new TestTimeProvider(_fixedTime);
        var evalStore = new InMemoryGovernanceEvaluationStore(_auditRepository);
        _handler = new EvaluateGovernanceRiskCommandHandler(_engine, evalStore, _timeProvider);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CreatesAuditRecord()
    {
        // Arrange
        var decisions = new List<DecisionHistoryObservationDto>
        {
            new("DEC-01", "SUBJ-101", "OFF-1", "INST-1", "Override", _fixedTime.UtcDateTime, true, false),
            new("DEC-02", "SUBJ-101", "OFF-1", "INST-1", "Override", _fixedTime.UtcDateTime.AddHours(1), true, false)
        };

        var inputDto = new GovernanceRiskEvaluationInputDto("SUBJ-101", decisions, null, null, null, null);
        var command = new EvaluateGovernanceRiskCommand("AssessLeaseRisk", inputDto);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SUBJ-101", result.SubjectId);
        Assert.Equal(25, result.OverallRiskScore);
        Assert.Equal("Moderate", result.Severity);
        Assert.True(result.RequiresHumanReview);
        Assert.NotEmpty(result.Disclaimer);

        Assert.Single(_auditRepository.AddedRecords);

        var log = _auditRepository.AddedRecords.First();
        Assert.Equal(EngineType.RiskAndCorruption, log.EngineType);
        Assert.Equal("AssessLeaseRisk", log.ActionName);
        Assert.Equal("ElevatedRiskDetected", log.Status);
        Assert.Contains("Evaluated subject 'SUBJ-101'", log.Details);
        Assert.Equal(_fixedTime.UtcDateTime, log.Timestamp);
    }

    [Fact]
    public async Task HandleAsync_AuditRecord_PrivacyProtected()
    {
        // Arrange: Pass detailed decision inputs
        var decisions = new List<DecisionHistoryObservationDto>
        {
            new("DEC-SECRET-01", "SUBJ-101", "OFFICER-CONFIDENTIAL", "INST-1", "Override", _fixedTime.UtcDateTime, true, false),
            new("DEC-SECRET-02", "SUBJ-101", "OFFICER-CONFIDENTIAL", "INST-1", "Override", _fixedTime.UtcDateTime.AddHours(1), true, false)
        };

        var inputDto = new GovernanceRiskEvaluationInputDto("SUBJ-101", decisions, null, null, null, null);
        var command = new EvaluateGovernanceRiskCommand("AssessLeaseRisk", inputDto);

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var log = _auditRepository.AddedRecords.First();

        // Audit log must NOT contain raw decision IDs or confidential officer names
        Assert.DoesNotContain("DEC-SECRET-01", log.Details);
        Assert.DoesNotContain("OFFICER-CONFIDENTIAL", log.Details);
        Assert.Contains("Evaluated 2 observations", log.Details);
    }

    [Fact]
    public async Task HandleAsync_SharedTimestamp_PropagatedToResultAndAudit()
    {
        // Arrange
        var inputDto = new GovernanceRiskEvaluationInputDto("SUBJ-101", null, null, null, null, null);
        var command = new EvaluateGovernanceRiskCommand("AssessRiskTimestampTest", inputDto);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var log = _auditRepository.AddedRecords.First();

        Assert.Equal(_fixedTime.UtcDateTime, result.EvaluationTimestamp);
        Assert.Equal(_fixedTime.UtcDateTime, log.Timestamp);
    }

    [Fact]
    public async Task HandleAsync_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.HandleAsync(null, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_EmptyActionName_ThrowsArgumentException(string invalidActionName)
    {
        // Arrange
        var inputDto = new GovernanceRiskEvaluationInputDto("SUBJ-101", null, null, null, null, null);
        var command = new EvaluateGovernanceRiskCommand(invalidActionName, inputDto);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }
}
