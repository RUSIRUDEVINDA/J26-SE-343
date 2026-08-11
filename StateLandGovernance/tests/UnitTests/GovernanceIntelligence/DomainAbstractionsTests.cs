using System;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class DomainAbstractionsTests
{
    [Fact]
    public void GovernanceAuditRecord_ShouldInitializeCorrectly_WhenValidArgumentsProvided()
    {
        // Arrange
        var engineType = EngineType.RegulatoryCompliance;
        var actionName = "EvaluateLeaseApproval";
        var status = "Compliant";
        var details = "Compliance check succeeded.";

        // Act
        var record = GovernanceAuditRecord.Create(engineType, actionName, status, details);

        // Assert
        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(engineType, record.EngineType);
        Assert.Equal(actionName, record.ActionName);
        Assert.Equal(status, record.Status);
        Assert.Equal(details, record.Details);
        Assert.True(record.Timestamp <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GovernanceAuditRecord_ShouldThrowArgumentException_WhenActionNameIsEmpty(string invalidActionName)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            GovernanceAuditRecord.Create(EngineType.RegulatoryCompliance, invalidActionName, "Status", "Details"));
    }

    [Fact]
    public void EngineTypeEnum_ShouldContainExactlySixSubEngines()
    {
        // Arrange & Act
        var enumValues = Enum.GetValues<EngineType>();

        // Assert
        Assert.Equal(6, enumValues.Length);
        Assert.Contains(EngineType.RegulatoryCompliance, enumValues);
        Assert.Contains(EngineType.GovernanceConflict, enumValues);
        Assert.Contains(EngineType.RiskAndCorruption, enumValues);
        Assert.Contains(EngineType.Consensus, enumValues);
        Assert.Contains(EngineType.ExplainableGovernance, enumValues);
        Assert.Contains(EngineType.ConditionalVerification, enumValues);
    }
}
