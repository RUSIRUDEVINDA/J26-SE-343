using System;
using System.Linq;
using System.Reflection;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class ArchitectureTests
{
    [Fact]
    public void DomainProject_ShouldNotDependOnInfrastructureOrExternalFrameworks()
    {
        // Arrange
        var domainAssembly = typeof(GovernanceAuditRecord).Assembly;

        // Act
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        // Assert: Domain should only refer to System/Microsoft base libraries or BuildingBlocks
        foreach (var assembly in referencedAssemblies)
        {
            var name = assembly.Name ?? string.Empty;

            Assert.False(name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase), 
                $"Domain must not reference EF Core: {name}");
            Assert.False(name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase), 
                $"Domain must not reference PostgreSQL provider (Npgsql): {name}");
            Assert.False(name.Contains("Fabric", StringComparison.OrdinalIgnoreCase), 
                $"Domain must not reference Hyperledger/Blockchain: {name}");
            Assert.False(name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase), 
                $"Domain must not reference AI: {name}");
            Assert.False(name.Contains("ML", StringComparison.OrdinalIgnoreCase), 
                $"Domain must not reference ML: {name}");
            Assert.False(name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase), 
                $"Domain must not reference Infrastructure: {name}");
        }
    }

    [Fact]
    public void ApplicationProject_ShouldNotDependOnConcreteInfrastructureOrExternalFrameworks()
    {
        // Arrange
        var applicationAssembly = typeof(GovernanceAuditRecordDto).Assembly;

        // Act
        var referencedAssemblies = applicationAssembly.GetReferencedAssemblies();

        // Assert: Application should not reference database providers, AI, or Blockchain
        foreach (var assembly in referencedAssemblies)
        {
            var name = assembly.Name ?? string.Empty;

            Assert.False(name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase), 
                $"Application must not reference EF Core: {name}");
            Assert.False(name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase), 
                $"Application must not reference PostgreSQL provider (Npgsql): {name}");
            Assert.False(name.Contains("Fabric", StringComparison.OrdinalIgnoreCase), 
                $"Application must not reference Hyperledger/Blockchain: {name}");
            Assert.False(name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase), 
                $"Application must not reference AI: {name}");
            Assert.False(name.Contains("ML", StringComparison.OrdinalIgnoreCase), 
                $"Application must not reference ML: {name}");
            Assert.False(name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) && !name.Contains("Shared.Infrastructure", StringComparison.OrdinalIgnoreCase), 
                $"Application must not reference concrete Infrastructure: {name}");
        }
    }
}
