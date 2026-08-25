using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Seeding;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class RegulatoryKnowledgeCatalogueIntegrationTests
{
    [Fact]
    public async Task InMemoryCatalogue_Returns24DefaultNpdRuleDefinitions()
    {
        var catalogue = new InMemoryComplianceRuleCatalogue();
        var rules = await catalogue.GetActiveRulesAsync(DateTime.UtcNow);

        Assert.Equal(24, rules.Count);
        Assert.Contains(rules, r => r.RuleCode == "NPD-LOC-001");
        Assert.Contains(rules, r => r.RuleCode == "NPD-FIN-001");
        Assert.Contains(rules, r => r.RuleCode == "NPD-ECO-001");
        Assert.Contains(rules, r => r.RuleCode == "NPD-EVD-001");
    }

    [Fact]
    public async Task Handler_CatalogueUnavailable_ReturnsRequiresReviewWithSysCatalogueFinding()
    {
        var dbOptions = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new GovernanceIntelligenceDbContext(dbOptions);

        var engine = new RegulatoryComplianceEngine();
        var auditRepo = new InMemoryGovernanceAuditRepository();
        var store = new InMemoryGovernanceEvaluationStore(auditRepo);
        var postgresCatalogue = new PostgresComplianceRuleCatalogue(dbContext); // Empty DB will throw

        var handler = new EvaluateComplianceCommandHandler(
            new InMemoryRegulatoryRuleProvider(),
            engine,
            store,
            postgresCatalogue);

        var command = new EvaluateComplianceCommand(
            Input: new ProposalComplianceInputDto("PROP-TEST-SYS001"),
            ActionName: "EvaluateCompliance"
        );

        var result = await handler.HandleAsync(command);

        Assert.Equal("RequiresReview", result.Status);
        Assert.Single(result.Findings);
        Assert.Equal("SYS-CATALOGUE-001", result.Findings[0].RuleCode);
        Assert.Equal("High", result.Findings[0].Severity);
        Assert.True(result.Findings[0].IsBlocking);
    }

    [Fact]
    public async Task PostgresCatalogue_SeededDatabase_Loads24ActiveRules()
    {
        var dbOptions = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new GovernanceIntelligenceDbContext(dbOptions);

        await RegulatoryKnowledgeSeeder.SeedAsync(dbContext);

        var catalogue = new PostgresComplianceRuleCatalogue(dbContext);
        var activeRules = await catalogue.GetActiveRulesAsync(DateTime.UtcNow);

        Assert.Equal(24, activeRules.Count);
        Assert.All(activeRules, r => Assert.True(r.Enabled));
        Assert.All(activeRules, r => Assert.Equal("Department of National Planning", r.SourceReference.SourceAuthority));
    }

    [Fact]
    public async Task PostgresCatalogue_MultipleActiveVersions_ThrowsInvalidOperationException()
    {
        var dbOptions = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new GovernanceIntelligenceDbContext(dbOptions);

        var source = new RegulatorySourceEntity
        {
            Id = Guid.NewGuid(),
            SourceType = "GovernmentOperationalManual",
            Authority = "Department of National Planning",
            DocumentTitle = "Operational Manual",
            DocumentVersion = "1.0",
            IsActive = true
        };
        dbContext.RegulatorySources.Add(source);

        // Add 2 active rules with same RuleCode "NPD-LOC-001"
        dbContext.ComplianceRules.Add(new ComplianceRuleEntity { Id = Guid.NewGuid(), RuleCode = "NPD-LOC-001", RuleVersion = "1.0", SourceId = source.Id, Enabled = true });
        dbContext.ComplianceRules.Add(new ComplianceRuleEntity { Id = Guid.NewGuid(), RuleCode = "NPD-LOC-001", RuleVersion = "2.0", SourceId = source.Id, Enabled = true });
        await dbContext.SaveChangesAsync();

        var catalogue = new PostgresComplianceRuleCatalogue(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => catalogue.GetActiveRulesAsync(DateTime.UtcNow));
    }

    [Fact]
    public void NpdRuleCatalogue_DisabledRule_IsSkipped()
    {
        var input = new ProposalComplianceInput("PROP-TEST-SKIP");
        var defaultRules = NpdRuleCatalogue.GetDefaultRuleDefinitions();

        // Disable NPD-LOC-001
        var modifiedRules = defaultRules.Select(r => r.RuleCode == "NPD-LOC-001" ? r with { Enabled = false } : r).ToList();

        var findings = NpdRuleCatalogue.EvaluateAll(input, modifiedRules);

        Assert.Equal(23, findings.Count);
        Assert.DoesNotContain(findings, f => f.RuleCode == "NPD-LOC-001");
    }

    [Fact]
    public void NpdRuleCatalogue_UnmappedRuleCode_ReturnsRequiresHumanReviewFinding()
    {
        var input = new ProposalComplianceInput("PROP-TEST-UNMAPPED");
        var customRule = new ComplianceRuleDefinition(
            "CUSTOM-RULE-999",
            "1.0",
            "Custom Section",
            RuleEvaluationType.Completeness,
            "Unknown custom calculation",
            "UnknownCalculationKey",
            "High",
            true,
            true,
            new RuleSourceMetadata(RuleSourceType.GovernmentOperationalManual, "Custom Authority", "Custom Doc", "Section 1"),
            new Dictionary<string, string>()
        );

        var findings = NpdRuleCatalogue.EvaluateAll(input, new[] { customRule });

        Assert.Single(findings);
        Assert.Equal("CUSTOM-RULE-999", findings[0].RuleCode);
        Assert.Equal(RuleResultStatus.RequiresHumanReview, findings[0].Status);
        Assert.True(findings[0].RequiresHumanReview);
        Assert.Contains("Unmapped calculation key 'UnknownCalculationKey'", findings[0].ObservedValueSummary);
    }
}
