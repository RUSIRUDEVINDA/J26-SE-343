using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class ArchitectureTests
{
    private static readonly string[] ForbiddenNamespaces = new[]
    {
        "LandIntelligence",
        "LeaseFeasibility",
        "WorkflowGovernance",
        "EntityFrameworkCore",
        "Npgsql",
        "PostgreSQL",
        "Neo4j",
        "PostGIS",
        "Fabric",
        "Hyperledger",
        "Nethereum",
        "OpenAI",
        "ML",
        "MediatR",
        "FluentValidation",
        "Serilog",
        "NetArchTest"
    };

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

            foreach (var forbidden in ForbiddenNamespaces)
            {
                // Skip checks for self-inclusions or safe framework base libraries
                if (forbidden == "Infrastructure") continue;
                Assert.False(name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"Domain must not reference {forbidden}: {name}");
            }

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

            foreach (var forbidden in ForbiddenNamespaces)
            {
                Assert.False(name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"Application must not reference {forbidden}: {name}");
            }

            Assert.False(name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase) && !name.Contains("Shared.Infrastructure", StringComparison.OrdinalIgnoreCase), 
                $"Application must not reference concrete Infrastructure: {name}");
        }
    }

    [Fact]
    public void GovernanceIntelligence_ShouldNotDependOnPeerModules()
    {
        // Arrange
        var domainAssembly = typeof(GovernanceAuditRecord).Assembly;
        var applicationAssembly = typeof(GovernanceAuditRecordDto).Assembly;
        var presentationAssembly = typeof(GovernanceIntelligenceController).Assembly;

        // Act
        var domainReferences = domainAssembly.GetReferencedAssemblies();
        var appReferences = applicationAssembly.GetReferencedAssemblies();
        var presentationReferences = presentationAssembly.GetReferencedAssemblies();

        var allReferences = domainReferences.Concat(appReferences).Concat(presentationReferences);

        // Assert
        foreach (var assembly in allReferences)
        {
            var name = assembly.Name ?? string.Empty;

            Assert.False(name.Contains("LandIntelligence", StringComparison.OrdinalIgnoreCase),
                $"GovernanceIntelligence must not reference LandIntelligence: {name}");
            Assert.False(name.Contains("LeaseFeasibility", StringComparison.OrdinalIgnoreCase),
                $"GovernanceIntelligence must not reference LeaseFeasibility: {name}");
            Assert.False(name.Contains("WorkflowGovernance", StringComparison.OrdinalIgnoreCase),
                $"GovernanceIntelligence must not reference WorkflowGovernance: {name}");
        }
    }

    [Fact]
    public void ProjectFiles_ShouldNotContainForbiddenPackageReferences()
    {
        // Explicit list of forbidden package families and identifiers
        var forbiddenPackageTokens = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "EntityFrameworkCore",
            "Npgsql",
            "Neo4j",
            "PostGIS",
            "NetTopologySuite",
            "Hyperledger",
            "Fabric",
            "OpenAI",
            "Microsoft.ML",
            "Nethereum",
            "MediatR",
            "FluentValidation",
            "Serilog",
            "NetArchTest"
        };

        // Act: Read and parse XML from all GovernanceIntelligence project files (.csproj)
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !dir.GetFiles("*.sln").Any())
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        var csprojFiles = Directory.GetFiles(dir.FullName, "*GovernanceIntelligence*.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(csprojFiles);

        foreach (var csprojPath in csprojFiles)
        {
            var fileName = Path.GetFileName(csprojPath);
            var doc = System.Xml.Linq.XDocument.Load(csprojPath);
            var packageRefs = doc.Descendants("PackageReference")
                .Select(el => el.Attribute("Include")?.Value)
                .Where(val => !string.IsNullOrEmpty(val))
                .ToList();

            foreach (var packageId in packageRefs)
            {
                Assert.NotNull(packageId);

                // Infrastructure project is allowed EF Core and Npgsql persistence packages
                if (fileName.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) &&
                    (packageId.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
                     packageId.StartsWith("Npgsql.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Test project is allowed EF Core InMemory provider package
                if (fileName.Contains(".UnitTests.", StringComparison.OrdinalIgnoreCase) &&
                    packageId.Equals("Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var forbidden in forbiddenPackageTokens)
                {
                    bool isForbiddenMatch =
                        packageId.Equals(forbidden, StringComparison.OrdinalIgnoreCase) ||
                        packageId.StartsWith(forbidden + ".", StringComparison.OrdinalIgnoreCase) ||
                        packageId.EndsWith("." + forbidden, StringComparison.OrdinalIgnoreCase) ||
                        packageId.Contains("." + forbidden + ".", StringComparison.OrdinalIgnoreCase) ||
                        packageId.Contains(forbidden, StringComparison.OrdinalIgnoreCase);

                    Assert.False(
                        isForbiddenMatch,
                        $"Project {fileName} contains forbidden package reference: {packageId} matching {forbidden}");
                }
            }
        }
    }
}
