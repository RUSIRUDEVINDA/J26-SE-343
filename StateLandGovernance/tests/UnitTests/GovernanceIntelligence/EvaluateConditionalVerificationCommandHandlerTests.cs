using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class EvaluateConditionalVerificationCommandHandlerTests
{
    private class SpyAuditRepository : IGovernanceAuditRepository
    {
        public GovernanceAuditRecord? CapturedRecord { get; private set; }
        public CancellationToken CapturedToken { get; private set; }

        public Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
        {
            CapturedRecord = record;
            CapturedToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<GovernanceAuditRecord?>(null);
        }
    }

    private class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _time;
        public FixedTimeProvider(DateTimeOffset time) => _time = time;
        public override DateTimeOffset GetUtcNow() => _time;
    }

    private readonly SpyAuditRepository _spyAuditRepo;
    private readonly FixedTimeProvider _fixedTimeProvider;
    private readonly ConditionalGovernanceVerificationEngine _engine;
    private readonly EvaluateConditionalVerificationCommandHandler _handler;

    public EvaluateConditionalVerificationCommandHandlerTests()
    {
        _spyAuditRepo = new SpyAuditRepository();
        var evalStore = new InMemoryGovernanceEvaluationStore(_spyAuditRepo);
        _fixedTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        _engine = new ConditionalGovernanceVerificationEngine();
        _handler = new EvaluateConditionalVerificationCommandHandler(_engine, evalStore, _fixedTimeProvider);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ExecutesVerificationAndLogsAudit()
    {
        var input = new ConditionalVerificationInputDto(
            SubjectId: "PARCEL-CMD-1",
            Conditions: new List<GovernanceConditionDto> { new("COND-1", "Condition 1") },
            Evidence: new List<VerificationEvidenceDto> { new("COND-1", ProvidedStatus: "Satisfied", Remarks: "Confidential Officer Note") }
        );

        var command = new EvaluateConditionalVerificationCommand("VerifyAction", input);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PARCEL-CMD-1", result.SubjectId);
        Assert.Equal("FullySatisfied", result.Outcome);
        Assert.NotNull(_spyAuditRepo.CapturedRecord);
        Assert.Equal(EngineType.ConditionalVerification, _spyAuditRepo.CapturedRecord.EngineType);
        Assert.Equal("VerifyAction", _spyAuditRepo.CapturedRecord.ActionName);
    }

    [Fact]
    public async Task HandleAsync_NullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.HandleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_EmptyActionName_ThrowsArgumentException()
    {
        var input = new ConditionalVerificationInputDto("PARCEL-CMD-1", new List<GovernanceConditionDto> { new("COND-1") });
        var command = new EvaluateConditionalVerificationCommand("  ", input);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_AuditCreation_CreatesRecordWithCorrectEngineTypeAndOutcome()
    {
        var input = new ConditionalVerificationInputDto(
            SubjectId: "PARCEL-CMD-1",
            Conditions: new List<GovernanceConditionDto> { new(ConditionId: "COND-1", IsMandatory: true) },
            Evidence: new List<VerificationEvidenceDto>()
        );

        var command = new EvaluateConditionalVerificationCommand("AuditTestAction", input);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("PendingEvidence", result.Outcome);
        Assert.NotNull(_spyAuditRepo.CapturedRecord);
        Assert.Equal(EngineType.ConditionalVerification, _spyAuditRepo.CapturedRecord.EngineType);
        Assert.Equal("PendingEvidence", _spyAuditRepo.CapturedRecord.Status);
    }

    [Fact]
    public async Task HandleAsync_AuditPrivacy_AuditDetailsContainOnlyAggregateMetrics()
    {
        var input = new ConditionalVerificationInputDto(
            SubjectId: "PARCEL-SENSITIVE-ID",
            Conditions: new List<GovernanceConditionDto> { new("COND-1") },
            Evidence: new List<VerificationEvidenceDto> { new("COND-1", Remarks: "SUPER_SECRET_OFFICER_REMARK_12345", IssuerOrAuthority: "SECRET_OFFICER_NAME") }
        );

        var command = new EvaluateConditionalVerificationCommand("PrivacyTestAction", input);

        await _handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(_spyAuditRepo.CapturedRecord);
        string auditDetails = _spyAuditRepo.CapturedRecord.Details;

        Assert.DoesNotContain("SUPER_SECRET_OFFICER_REMARK_12345", auditDetails);
        Assert.DoesNotContain("SECRET_OFFICER_NAME", auditDetails);
        Assert.Contains("Evaluated conditional verification across", auditDetails);
    }

    [Fact]
    public async Task HandleAsync_CancellationTokenPropagation_PassesTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var input = new ConditionalVerificationInputDto(
            SubjectId: "PARCEL-CMD-1",
            Conditions: new List<GovernanceConditionDto> { new("COND-1") },
            Evidence: new List<VerificationEvidenceDto> { new("COND-1") }
        );

        var command = new EvaluateConditionalVerificationCommand("TokenTestAction", input);

        await _handler.HandleAsync(command, token);

        Assert.Equal(token, _spyAuditRepo.CapturedToken);
    }
}
