using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class CompliancePersistenceRedesignPhaseATests
{
    private static GovernanceIntelligenceDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GovernanceIntelligenceDbContext(options);
    }

    [Fact]
    public void MapToEntities_SerializesAll16RichFindingFieldsIntoFindingsJson()
    {
        // Arrange
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "EvaluateNpdProposalCompliance",
            "InsufficientInformation",
            "Findings: 1, Violations: 0, Conditions: 1");

        var finding = new ComplianceFinding(
            RuleCode: "NPD-TEST-001",
            RuleVersion: "1.2.3",
            Category: RuleEvaluationType.EconomicFinancialAppraisal,
            Applicability: RuleApplicability.Applicable,
            Status: RuleResultStatus.InsufficientInformation,
            Severity: "High",
            IsBlocking: true,
            RequiresHumanReview: true,
            ObservedValueSummary: "Missing valuation summary",
            ExpectedRequirement: "Valuation required by NPD Section 4.2",
            EvidenceStatus: EvidenceStatus.Missing,
            CalculationStatus: CalculationStatus.MissingInputs,
            SourceReference: new RuleSourceMetadata(RuleSourceType.GovernmentOperationalManual, "NPD Authority", "NPD Manual 2026", "Section 4.2"),
            RecommendedAction: "Provide official valuation report"
        );

        var complianceResult = new ComplianceResult(
            ComplianceStatus.InsufficientInformation,
            new[] { finding },
            "DET-HASH-TEST-12345",
            DateTime.UtcNow
        );

        // Act
        var (auditEntity, evalEntity) = ComplianceEvaluationMapper.MapToEntities(
            auditRecord,
            complianceResult,
            "EvaluateNpdProposalCompliance",
            "PROP-TEST-999");

        // Assert
        Assert.NotNull(evalEntity);
        Assert.Equal("PROP-TEST-999", evalEntity.ProposalId);
        Assert.Equal("DET-HASH-TEST-12345", evalEntity.DeterministicEvaluationId);
        Assert.Equal("1.0.0", evalEntity.RuleSetVersion);
        Assert.NotNull(evalEntity.FindingsJson);

        var deserializedDtos = JsonSerializer.Deserialize<List<ComplianceFindingDto>>(evalEntity.FindingsJson!);
        Assert.NotNull(deserializedDtos);
        Assert.Single(deserializedDtos!);

        var dto = deserializedDtos![0];
        Assert.Equal("NPD-TEST-001", dto.RuleCode);
        Assert.Equal("1.2.3", dto.RuleVersion);
        Assert.Equal("EconomicFinancialAppraisal", dto.Category);
        Assert.Equal("Applicable", dto.Applicability);
        Assert.Equal("InsufficientInformation", dto.Status);
        Assert.Equal("High", dto.Severity);
        Assert.True(dto.IsBlocking);
        Assert.True(dto.RequiresHumanReview);
        Assert.Equal("Missing valuation summary", dto.ObservedValueSummary);
        Assert.Equal("Valuation required by NPD Section 4.2", dto.ExpectedRequirement);
        Assert.Equal("Missing", dto.EvidenceStatus);
        Assert.Equal("MissingInputs", dto.CalculationStatus);
        Assert.Equal("NPD Authority", dto.SourceAuthority);
        Assert.Equal("NPD Manual 2026", dto.SourceDocument);
        Assert.Equal("Section 4.2", dto.SourceSection);
        Assert.Equal("Provide official valuation report", dto.RecommendedAction);
    }

    [Fact]
    public void MapToEntities_WithNullProposalId_PersistsNullWithoutFakeFallback()
    {
        // Arrange
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "EvaluateCompliance",
            "Compliant",
            "Legacy evaluation");

        var complianceResult = new ComplianceResult(
            ComplianceStatus.Compliant,
            Array.Empty<Violation>(),
            Array.Empty<ComplianceCondition>()
        );

        // Act
        var (_, evalEntity) = ComplianceEvaluationMapper.MapToEntities(
            auditRecord,
            complianceResult,
            "EvaluateCompliance",
            proposalId: null);

        // Assert
        Assert.Null(evalEntity.ProposalId);
        Assert.NotEqual("LEGACY-PROP-001", evalEntity.ProposalId);
        Assert.NotEqual("PROP-NPD", evalEntity.ProposalId);
    }

    [Fact]
    public async Task StoreComplianceEvaluationAsync_PersistsRichSnapshotWithoutLegacyChildTables()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresGovernanceEvaluationStore(dbContext);

        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "EvaluateNpdProposalCompliance",
            "NonCompliant",
            "Findings: 1, Violations: 1, Conditions: 0");

        var finding = new ComplianceFinding(
            RuleCode: "NPD-FIN-001",
            RuleVersion: "1.0",
            Category: RuleEvaluationType.Financing,
            Applicability: RuleApplicability.Applicable,
            Status: RuleResultStatus.NonCompliant,
            Severity: "Critical",
            IsBlocking: true,
            RequiresHumanReview: false,
            ObservedValueSummary: "Funding Gap: 400000",
            ExpectedRequirement: "Total Financing must equal Budget",
            EvidenceStatus: EvidenceStatus.Verified,
            CalculationStatus: CalculationStatus.Calculated,
            SourceReference: new RuleSourceMetadata(RuleSourceType.GovernmentOperationalManual, "NPD Authority", "Manual", "Sec 5"),
            RecommendedAction: "Resolve funding gap of 400,000.00"
        );

        var violation = new Violation("NPD-FIN-001", "Funding Gap of 400,000.00");
        var complianceResult = new ComplianceResult(
            ComplianceStatus.NonCompliant,
            new[] { finding },
            "DET-HASH-FIN-001",
            DateTime.UtcNow,
            violations: new[] { violation },
            conditions: Array.Empty<ComplianceCondition>()
        );

        // Act
        await store.StoreComplianceEvaluationAsync(
            auditRecord,
            complianceResult,
            "EvaluateNpdProposalCompliance",
            "PROP-COL-2026-FIN",
            default);

        // Assert
        var savedEval = await dbContext.ComplianceEvaluations.FirstOrDefaultAsync();

        Assert.NotNull(savedEval);
        Assert.Equal("PROP-COL-2026-FIN", savedEval!.ProposalId);
        Assert.Equal("DET-HASH-FIN-001", savedEval.DeterministicEvaluationId);
        Assert.Equal("1.0.0", savedEval.RuleSetVersion);
        Assert.NotNull(savedEval.FindingsJson);
        Assert.Equal("NonCompliant", savedEval.Status);

        // Verify EF model no longer contains legacy child navigations
        var entityType = dbContext.Model.FindEntityType(typeof(ComplianceEvaluationEntity));
        Assert.NotNull(entityType);
        Assert.Null(entityType!.FindNavigation("Violations"));
        Assert.Null(entityType!.FindNavigation("Conditions"));
    }

    [Fact]
    public void DbContextConfiguration_ConfiguresRegulatoryKnowledgeEntitiesAndIndexes()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();

        var source = new RegulatorySourceEntity
        {
            Id = Guid.NewGuid(),
            SourceType = "NpdOperationalManual",
            Authority = "National Planning Department",
            DocumentTitle = "NPD Proposal Manual",
            DocumentVersion = "1.0",
            IsActive = true
        };

        var rule = new ComplianceRuleEntity
        {
            Id = Guid.NewGuid(),
            RuleCode = "NPD-BUD-001",
            RuleVersion = "1.0",
            SourceId = source.Id,
            SourceSection = "Section 3.1",
            Category = "Financing",
            Description = "Project budget verification",
            RuleType = "CrossFieldConsistency",
            Severity = "High",
            IsBlocking = true,
            Enabled = true
        };

        var param = new RuleParameterEntity
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            ParameterName = "MinBudgetThreshold",
            ParameterValue = "100000",
            Unit = "LKR"
        };

        dbContext.RegulatorySources.Add(source);
        dbContext.ComplianceRules.Add(rule);
        dbContext.RuleParameters.Add(param);
        dbContext.SaveChanges();

        // Assert
        Assert.Equal(1, dbContext.RegulatorySources.Count());
        Assert.Equal(1, dbContext.ComplianceRules.Count());
        Assert.Equal(1, dbContext.RuleParameters.Count());

        var savedRule = dbContext.ComplianceRules
            .Include(r => r.Source)
            .Include(r => r.Parameters)
            .FirstOrDefault();

        Assert.NotNull(savedRule);
        Assert.Equal("NPD-BUD-001", savedRule!.RuleCode);
        Assert.Equal("1.0", savedRule.RuleVersion);
        Assert.Equal("Section 3.1", savedRule.SourceSection);
        Assert.NotNull(savedRule.Source);
        Assert.Equal("NPD Proposal Manual", savedRule.Source!.DocumentTitle);
        Assert.Single(savedRule.Parameters);
        Assert.Equal("MinBudgetThreshold", savedRule.Parameters[0].ParameterName);
    }
}
