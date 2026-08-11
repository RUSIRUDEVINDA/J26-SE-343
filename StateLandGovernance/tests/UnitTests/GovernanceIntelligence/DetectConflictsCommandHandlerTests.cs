using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class DetectConflictsCommandHandlerTests
{
    private readonly DetectConflictsCommandHandler _handler;
    private readonly SpyAuditRepository _auditRepository;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTimeOffset _fixedTime = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _time;
        public FakeTimeProvider(DateTimeOffset time) => _time = time;
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

    public DetectConflictsCommandHandlerTests()
    {
        _auditRepository = new SpyAuditRepository();
        var conflictEngine = new GovernanceConflictEngine();
        _timeProvider = new FakeTimeProvider(_fixedTime);

        _handler = new DetectConflictsCommandHandler(
            conflictEngine,
            _auditRepository,
            _timeProvider);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNoConflictsResult_WhenDecisionsAreCompatible()
    {
        // Arrange
        var command = new DetectConflictsCommand(
            ActionName: "AssessCompatibleDecisions",
            Decisions: new List<GovernanceDecisionSnapshotDto>
            {
                new("DEC_A", "PARCEL_1", "UDA", "National", "Approval", "Agricultural", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Law_1"),
                new("DEC_B", "PARCEL_2", "LocalCouncil", "Local", "Approval", "Commercial", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "ByLaw_1")
            }
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal("NoConflicts", result.Status);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnConflictsResult_WhenDecisionsConflict()
    {
        // Arrange
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var command = new DetectConflictsCommand(
            ActionName: "AssessOverlappingDecisions",
            Decisions: new List<GovernanceDecisionSnapshotDto>
            {
                new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
                new("DEC_2", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
            }
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal("ConflictsDetected", result.Status);
        Assert.Single(result.Conflicts);
        Assert.Equal("ContradictoryDecisions", result.Conflicts[0].ConflictType);
        Assert.Contains("DEC_1", result.Conflicts[0].InvolvedDecisionIds);
        Assert.Contains("DEC_2", result.Conflicts[0].InvolvedDecisionIds);
    }

    [Fact]
    public async Task HandleAsync_ShouldLogAuditMetadataAndSharedTimestamp()
    {
        // Arrange
        var from = DateTime.UtcNow;
        var to = from.AddYears(1);
        var command = new DetectConflictsCommand(
            ActionName: "AuditLogTestAction",
            Decisions: new List<GovernanceDecisionSnapshotDto>
            {
                new("DEC_1", "PARCEL_1", "UDA", "National", "Approval", "Industrial", from, to, "Law_A"),
                new("DEC_2", "PARCEL_1", "UDA", "National", "Rejection", "Industrial", from, to, "Law_A")
            }
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Single(_auditRepository.AddedRecords);
        var audit = _auditRepository.AddedRecords[0];
        Assert.Equal(EngineType.GovernanceConflict, audit.EngineType);
        Assert.Equal("AuditLogTestAction", audit.ActionName);
        Assert.Equal("ConflictsDetected", audit.Status);
        Assert.Equal(_fixedTime.UtcDateTime, audit.Timestamp);

        // Assert every returned conflict has the fixed timestamp
        Assert.NotEmpty(result.Conflicts);
        foreach (var conflict in result.Conflicts)
        {
            Assert.Equal(_fixedTime.UtcDateTime, conflict.DetectionTimestamp);
        }

        // Explicitly assert the audit timestamp equals the returned conflict timestamp
        Assert.Equal(audit.Timestamp, result.Conflicts[0].DetectionTimestamp);
    }

    // Narrow Pass handler-level verification tests

    [Fact]
    public async Task HandleAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // 1. Null command
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.HandleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowArgumentException_WhenDecisionsIsNull()
    {
        // 2. Null Decisions collection
        var command = new DetectConflictsCommand("Action", null!);
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowArgumentException_WhenDecisionsContainsNullElement()
    {
        // 3. Null element inside decisions
        var command = new DetectConflictsCommand("Action", new List<GovernanceDecisionSnapshotDto> { null! });
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNoConflicts_WhenDecisionsIsEmpty()
    {
        // 4. Empty decisions collection is supported and returns success with 0 conflicts detected
        var command = new DetectConflictsCommand("Action", new List<GovernanceDecisionSnapshotDto>());
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("NoConflicts", result.Status);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task HandleAsync_ShouldVerifyAllAuditMetadataMetrics()
    {
        // Assert: Engine invocation, mapping, repository invocation, counts, highest severity, shared timestamp, exact token propagation
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var from = DateTime.UtcNow;
        var to = from.AddYears(1);

        // Recognizable values
        var decId1 = "DEC_UNIQUE_XYZ_1";
        var decId2 = "DEC_UNIQUE_XYZ_2";
        var subjectId = "PARCEL_UNIQUE_999";
        var inst1 = "MinistryOfZoning_Special";
        var inst2 = "LocalCouncil_Special";
        var regRef = "Law_Special_Reference_999";
        var landUse = "IND_ZONING_CODE";
        var mandateKey = "EXCLUSIVE_MANDATE_ZONING";

        var command = new DetectConflictsCommand(
            ActionName: "AuditLogMetricsAction",
            Decisions: new List<GovernanceDecisionSnapshotDto>
            {
                new(decId1, subjectId, inst1, "National", "Approval", "Industrial", from, to, regRef, 
                    IncompatibleRegulatoryReferences: new[] { "Law_B" },
                    MandateKey: mandateKey, MandateMode: "Exclusive",
                    LandUseCode: landUse, IncompatibleLandUseCodes: new[] { "RES" }),
                new(decId2, subjectId, inst2, "National", "Rejection", "Industrial", from, to, "Law_B",
                    LandUseCode: "RES")
            }
        );

        var result = await _handler.HandleAsync(command, token);

        // Verify token propagation
        Assert.Equal(token, _auditRepository.CapturedToken);

        // Verify audit details contains no snapshots, only metadata (eval occurred, counts, severity)
        var audit = _auditRepository.AddedRecords.Last();
        Assert.Contains("Evaluation occurred", audit.Details);
        Assert.Contains("Evaluated 2 decisions", audit.Details);
        Assert.Contains("Detected conflicts: 3", audit.Details);
        Assert.Contains("Highest severity: Critical", audit.Details);

        // Assert it does not leak raw snapshots or lists
        Assert.DoesNotContain(decId1, audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(decId2, audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(subjectId, audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(inst1, audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(inst2, audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(regRef, audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(landUse, audit.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(mandateKey, audit.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldVerifyNoneSeverity_WhenNoConflictsAreFound()
    {
        var command = new DetectConflictsCommand(
            ActionName: "AuditLogNoneAction",
            Decisions: new List<GovernanceDecisionSnapshotDto>()
        );

        await _handler.HandleAsync(command, CancellationToken.None);

        var audit = _auditRepository.AddedRecords.Last();
        Assert.Contains("Highest severity: None", audit.Details);
        Assert.Contains("Detected conflicts: 0", audit.Details);
    }
}
