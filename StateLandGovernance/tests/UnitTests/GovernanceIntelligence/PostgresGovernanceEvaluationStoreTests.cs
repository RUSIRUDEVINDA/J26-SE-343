using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class PostgresGovernanceEvaluationStoreTests
{
    private GovernanceIntelligenceDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GovernanceIntelligenceDbContext(options);
    }

    [Fact]
    public async Task StoreComplianceEvaluationAsync_PersistsAuditAndStructuredEvaluationEntities()
    {
        using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresGovernanceEvaluationStore(dbContext);

        var audit = GovernanceAuditRecord.Create(EngineType.RegulatoryCompliance, "CheckCompliance", "Compliant", "All rules passed");
        var result = new ComplianceResult(
            ComplianceStatus.Compliant,
            new[] { new Violation("RULE-101", "Violation 1") },
            new[] { new ComplianceCondition("Condition 1", DateTime.UtcNow.AddDays(30)) });

        await store.StoreComplianceEvaluationAsync(audit, result, "CheckCompliance");

        var persistedAudit = await dbContext.GovernanceAuditRecords.FirstOrDefaultAsync(a => a.Id == audit.Id);
        Assert.NotNull(persistedAudit);
        Assert.Equal("CheckCompliance", persistedAudit.ActionName);

        var persistedEval = await dbContext.ComplianceEvaluations
            .FirstOrDefaultAsync(e => e.AuditRecordId == audit.Id);

        Assert.NotNull(persistedEval);
        Assert.Equal("Compliant", persistedEval!.Status);
        Assert.Equal("CheckCompliance", persistedEval.ActionName);
    }

    [Fact]
    public async Task StoreConflictEvaluationAsync_PersistsAuditAndConflictFindings()
    {
        using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresGovernanceEvaluationStore(dbContext);

        var audit = GovernanceAuditRecord.Create(EngineType.GovernanceConflict, "DetectConflicts", "ConflictsDetected", "Found 1 conflict");
        var conflicts = new[]
        {
            new DetectedConflict(
                "CONF-001",
                "MandateOverlap",
                "High",
                "Active",
                new[] { "DEC-1" },
                new[] { "INST-A" },
                "SUBJ-100",
                "Overlap explanation",
                "RULE-CONF",
                "Review mandate",
                DateTime.UtcNow)
        };

        await store.StoreConflictEvaluationAsync(audit, conflicts, "DetectConflicts", 2);

        var persistedEval = await dbContext.ConflictEvaluations
            .Include(e => e.Findings)
            .FirstOrDefaultAsync(e => e.AuditRecordId == audit.Id);

        Assert.NotNull(persistedEval);
        Assert.Equal(2, persistedEval.TotalEvaluatedDecisions);
        Assert.Equal(1, persistedEval.DetectedConflictsCount);
        Assert.Equal("High", persistedEval.HighestSeverity);
        Assert.Single(persistedEval.Findings);
        Assert.Equal("CONF-001", persistedEval.Findings[0].ConflictId);
    }

    [Fact]
    public async Task SharedInMemoryAuditRepository_ForwardsAuditRecordsFromEvaluationStore()
    {
        var auditRepo = new InMemoryGovernanceAuditRepository();
        var evalStore = new InMemoryGovernanceEvaluationStore(auditRepo);

        var audit = GovernanceAuditRecord.Create(EngineType.RegulatoryCompliance, "TestInMemory", "Success", "Details");
        var result = new ComplianceResult(ComplianceStatus.Compliant, Array.Empty<Violation>(), Array.Empty<ComplianceCondition>());

        await evalStore.StoreComplianceEvaluationAsync(audit, result, "TestInMemory");

        var retrievedAudit = await auditRepo.GetByIdAsync(audit.Id);
        Assert.NotNull(retrievedAudit);
        Assert.Equal("TestInMemory", retrievedAudit.ActionName);
    }

    [Fact]
    public async Task StoreMethods_PropagateCancellationToken()
    {
        using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresGovernanceEvaluationStore(dbContext);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var audit = GovernanceAuditRecord.Create(EngineType.RegulatoryCompliance, "CancelledAction", "Status", "Details");
        var result = new ComplianceResult(ComplianceStatus.Compliant, Array.Empty<Violation>(), Array.Empty<ComplianceCondition>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.StoreComplianceEvaluationAsync(audit, result, "CancelledAction", cts.Token));
    }
}
