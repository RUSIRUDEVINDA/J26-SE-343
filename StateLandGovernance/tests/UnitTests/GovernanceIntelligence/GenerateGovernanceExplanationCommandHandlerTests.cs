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
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GenerateGovernanceExplanationCommandHandlerTests
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

        public Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
        {
            AddedRecords.Add(record);
            CapturedToken = cancellationToken;
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

    private readonly SpyAuditRepository _spyAuditRepository;
    private readonly TestTimeProvider _testTimeProvider;
    private readonly ExplainableGovernanceEngine _domainEngine;
    private readonly GenerateGovernanceExplanationCommandHandler _handler;
    private readonly DateTimeOffset _testTime;

    public GenerateGovernanceExplanationCommandHandlerTests()
    {
        _spyAuditRepository = new SpyAuditRepository();
        _testTime = new DateTimeOffset(2026, 8, 14, 11, 0, 0, TimeSpan.Zero);
        _testTimeProvider = new TestTimeProvider(_testTime);
        _domainEngine = new ExplainableGovernanceEngine();

        _handler = new GenerateGovernanceExplanationCommandHandler(
            _domainEngine,
            _spyAuditRepository,
            _testTimeProvider);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ShouldCreateAuditRecordAndReturnResult()
    {
        var inputDto = new GovernanceExplanationInputDto(
            "PARCEL-201",
            new ComplianceExplanationEvidenceDto(
                "NonCompliant",
                new[] { new ComplianceViolationSummaryDto("RULE-DURATION", "LeaseTerms", "Duration exceeds max limit.") },
                new[] { "Environmental clearance missing" }),
            null,
            null);

        var command = new GenerateGovernanceExplanationCommand("GenerateLeaseExplanation", inputDto);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PARCEL-201", result.SubjectId);
        Assert.Equal("High", result.OverallSeverity);
        Assert.True(result.RequiresHumanReview);
        Assert.Equal(2, result.Explanations.Count);
        Assert.Equal(_testTime.UtcDateTime, result.EvaluationTimestamp);

        // Verify Audit Repository Integration
        Assert.Single(_spyAuditRepository.AddedRecords);
        var audit = _spyAuditRepository.AddedRecords[0];
        Assert.Equal(EngineType.ExplainableGovernance, audit.EngineType);
        Assert.Equal("GenerateLeaseExplanation", audit.ActionName);
        Assert.Equal("ElevatedGovernanceAttentionRequired", audit.Status);
        Assert.Equal(_testTime.UtcDateTime, audit.Timestamp);
    }

    [Fact]
    public async Task HandleAsync_AuditRecord_ShouldContainOnlySafeAggregateMetadata()
    {
        var inputDto = new GovernanceExplanationInputDto(
            "SECRET-SUBJECT-999",
            new ComplianceExplanationEvidenceDto(
                "NonCompliant",
                new[] { new ComplianceViolationSummaryDto("R1", "Cat", "Confidential details") },
                null),
            null,
            null);

        var command = new GenerateGovernanceExplanationCommand("ExplainAction", inputDto);

        await _handler.HandleAsync(command, CancellationToken.None);

        Assert.Single(_spyAuditRepository.AddedRecords);
        var audit = _spyAuditRepository.AddedRecords[0];
        Assert.DoesNotContain("SECRET-SUBJECT-999", audit.Details);
        Assert.DoesNotContain("Confidential details", audit.Details);
        Assert.Contains("Evaluation completed across 1 source engines", audit.Details);
        Assert.Contains("Generated 1 explanation items", audit.Details);
    }

    [Fact]
    public async Task HandleAsync_CancellationTokenPassed_ShouldPropagateToken()
    {
        var cts = new CancellationTokenSource();
        var inputDto = new GovernanceExplanationInputDto("PARCEL-202", null, null, null);
        var command = new GenerateGovernanceExplanationCommand("TestCancel", inputDto);

        await _handler.HandleAsync(command, cts.Token);

        Assert.Equal(cts.Token, _spyAuditRepository.CapturedToken);
    }

    [Fact]
    public async Task HandleAsync_NullCommand_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.HandleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_EmptyActionName_ShouldThrowArgumentException()
    {
        var inputDto = new GovernanceExplanationInputDto("PARCEL-203", null, null, null);
        var command = new GenerateGovernanceExplanationCommand("", inputDto);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_NullInput_ShouldThrowArgumentException()
    {
        var command = new GenerateGovernanceExplanationCommand("Action", null!);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_EmptySubjectId_ShouldThrowArgumentException()
    {
        var inputDto = new GovernanceExplanationInputDto("  ", null, null, null);
        var command = new GenerateGovernanceExplanationCommand("Action", inputDto);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }
}
