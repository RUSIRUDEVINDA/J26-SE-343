namespace StateLandGovernance.ArchitectureTests;

using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Application.Commands;

public sealed class WorkflowGovernanceBoundaryTests
{
    [Fact]
    public void WorkflowDomain_RemainsIndependentOfPersistenceHttpAndWebFrameworks()
    {
        var references = typeof(LeaseCase).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(references, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || n == "System.Net.Http" ||
            n.Contains("Infrastructure", StringComparison.Ordinal) || n.Contains("GovernanceIntelligence", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkflowApplication_UsesItsPortsRatherThanPeerModuleInternals()
    {
        var references = typeof(PostWorkflowGovernanceService).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(references, n => n.Contains("GovernanceIntelligence", StringComparison.Ordinal) ||
            n.Contains("LandIntelligence", StringComparison.Ordinal) || n.Contains("LeaseFeasibility", StringComparison.Ordinal) ||
            n.Contains("Infrastructure", StringComparison.Ordinal) || n.Contains("Presentation", StringComparison.Ordinal));
    }

    [Fact]
    public void GovernanceIntelligenceDomain_DoesNotDependOnWorkflowGovernance()
    {
        var references = typeof(StateLandGovernance.GovernanceIntelligence.Domain.Entities.GovernanceAuditRecord)
            .Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(references, n => n.Contains("WorkflowGovernance", StringComparison.Ordinal));
    }
}
