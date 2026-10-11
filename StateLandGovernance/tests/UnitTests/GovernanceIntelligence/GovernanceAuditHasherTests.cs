using System;
using System.Globalization;
using System.Threading;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GovernanceAuditHasherTests
{
    private readonly Guid _testId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private readonly DateTime _testTimestamp = new(2026, 10, 9, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ComputeCanonicalHash_IdenticalRecords_ProduceIdenticalHashes()
    {
        // Arrange
        var record1 = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "EvaluateLeaseDuration",
            "Satisfied",
            "Lease duration 30 years is compliant.",
            _testTimestamp);

        var record2 = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "EvaluateLeaseDuration",
            "Satisfied",
            "Lease duration 30 years is compliant.",
            _testTimestamp);

        // Overwrite ID to be identical for equality test
        var fixedRecord1 = RecreateRecordWithFixedId(_testId, record1);
        var fixedRecord2 = RecreateRecordWithFixedId(_testId, record2);

        // Act
        var hash1 = GovernanceAuditHasher.ComputeCanonicalHash(fixedRecord1);
        var hash2 = GovernanceAuditHasher.ComputeCanonicalHash(fixedRecord2);

        // Assert
        Assert.NotNull(hash1);
        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeCanonicalHash_DifferentAction_ProducesDifferentHash()
    {
        // Arrange
        var record1 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "ActionA", "Status", "Details", _testTimestamp));
        var record2 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "ActionB", "Status", "Details", _testTimestamp));

        // Act & Assert
        Assert.NotEqual(
            GovernanceAuditHasher.ComputeCanonicalHash(record1),
            GovernanceAuditHasher.ComputeCanonicalHash(record2));
    }

    [Fact]
    public void ComputeCanonicalHash_DifferentStatus_ProducesDifferentHash()
    {
        // Arrange
        var record1 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "Action", "Satisfied", "Details", _testTimestamp));
        var record2 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "Action", "Breached", "Details", _testTimestamp));

        // Act & Assert
        Assert.NotEqual(
            GovernanceAuditHasher.ComputeCanonicalHash(record1),
            GovernanceAuditHasher.ComputeCanonicalHash(record2));
    }

    [Fact]
    public void ComputeCanonicalHash_DifferentTimestamp_ProducesDifferentHash()
    {
        // Arrange
        var record1 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "Action", "Status", "Details", _testTimestamp));
        var record2 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "Action", "Status", "Details", _testTimestamp.AddSeconds(1)));

        // Act & Assert
        Assert.NotEqual(
            GovernanceAuditHasher.ComputeCanonicalHash(record1),
            GovernanceAuditHasher.ComputeCanonicalHash(record2));
    }

    [Fact]
    public void ComputeCanonicalHash_DifferentDetails_ProducesDifferentHash()
    {
        // Arrange
        var record1 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "Action", "Status", "Details A", _testTimestamp));
        var record2 = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance, "Action", "Status", "Details B", _testTimestamp));

        // Act & Assert
        Assert.NotEqual(
            GovernanceAuditHasher.ComputeCanonicalHash(record1),
            GovernanceAuditHasher.ComputeCanonicalHash(record2));
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void ComputeCanonicalHash_IsCultureIndependent(string cultureName)
    {
        // Arrange
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            var record = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
                EngineType.GovernanceConflict,
                "institutional-check",
                "resolved",
                "Conflicts checked: 0",
                _testTimestamp));

            var invariantHash = GovernanceAuditHasher.ComputeCanonicalHash(record);

            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);

            var cultureSpecificHash = GovernanceAuditHasher.ComputeCanonicalHash(record);

            // Assert
            Assert.Equal(invariantHash, cultureSpecificHash);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void BuildCanonicalPayload_StartsWithAuditV1AndContainsExpectedFields()
    {
        // Arrange
        var record = RecreateRecordWithFixedId(_testId, GovernanceAuditRecord.Create(
            EngineType.RiskAndCorruption,
            "EvaluateRisk",
            "Evaluated",
            "Indicators detected: 1",
            _testTimestamp));

        // Act
        var payload = GovernanceAuditHasher.BuildCanonicalPayload(record);

        // Assert
        Assert.StartsWith("AUDIT-V1|", payload);
        Assert.Contains(_testId.ToString("D").ToLowerInvariant(), payload);
        Assert.Contains("EVALUATERISK", payload);
        Assert.Contains("EVALUATED", payload);
    }

    private static GovernanceAuditRecord RecreateRecordWithFixedId(Guid id, GovernanceAuditRecord template)
    {
        var constructor = typeof(GovernanceAuditRecord).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(Guid), typeof(EngineType), typeof(string), typeof(string), typeof(string), typeof(DateTime) },
            null)!;

        return (GovernanceAuditRecord)constructor.Invoke(new object[]
        {
            id,
            template.EngineType,
            template.ActionName,
            template.Status,
            template.Details,
            template.Timestamp
        });
    }
}
