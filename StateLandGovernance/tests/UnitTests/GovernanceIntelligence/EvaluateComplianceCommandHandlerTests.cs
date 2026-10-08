using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.Application;

public class EvaluateComplianceCommandHandlerTests
{
    private readonly EvaluateComplianceCommandHandler _handler;
    private readonly InMemoryRegulatoryRuleProvider _ruleProvider;
    private readonly InMemoryGovernanceAuditRepository _auditRepository;

    public EvaluateComplianceCommandHandlerTests()
    {
        _ruleProvider = new InMemoryRegulatoryRuleProvider();
        _auditRepository = new InMemoryGovernanceAuditRepository();
        var evalStore = new InMemoryGovernanceEvaluationStore(_auditRepository);
        var complianceEngine = new RegulatoryComplianceEngine();

        _handler = new EvaluateComplianceCommandHandler(
            _ruleProvider,
            complianceEngine,
            evalStore);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCompliantResult_WhenInputsAreValid()
    {
        // Arrange
        var command = new EvaluateComplianceCommand(
            ActionName: "ApproveStandardLease",
            LeaseDurationYears: 20,
            ProposedUse: "Conservation",
            LeaseAmount: 2500.00m,
            ZoningArea: "ForestReserve"
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal("Compliant", result.Status);
        Assert.Empty(result.Violations);
        Assert.Empty(result.Conditions);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNonCompliantResult_WhenInputsViolateRules()
    {
        // Arrange
        var command = new EvaluateComplianceCommand(
            ActionName: "ApproveIllegalIndustrialLease",
            LeaseDurationYears: 120, // Violation (exceeds 99 years)
            ProposedUse: "Industrial", // Violation (prohibited in Residential)
            LeaseAmount: -100.00m, // Violation (negative amount)
            ZoningArea: "Residential"
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal("NonCompliant", result.Status);
        Assert.Equal(3, result.Violations.Count);
        Assert.Empty(result.Conditions);
    }
}
