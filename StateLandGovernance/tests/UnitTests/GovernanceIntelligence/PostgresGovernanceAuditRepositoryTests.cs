using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class PostgresGovernanceAuditRepositoryTests
{
    private static GovernanceIntelligenceDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new GovernanceIntelligenceDbContext(options);
    }

    [Fact]
    public async Task AddAsync_StoresGovernanceAuditRecord_Successfully()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repository = new PostgresGovernanceAuditRepository(dbContext);
        var record = GovernanceAuditRecord.Create(
            EngineType.ConditionalVerification,
            "VerifyConditions",
            "FullySatisfied",
            "Evaluated 3 mandatory conditions.");

        // Act
        await repository.AddAsync(record);

        // Assert
        var storedEntity = await dbContext.GovernanceAuditRecords.FirstOrDefaultAsync(x => x.Id == record.Id);
        Assert.NotNull(storedEntity);
        Assert.Equal((int)EngineType.ConditionalVerification, storedEntity.EngineType);
        Assert.Equal("VerifyConditions", storedEntity.ActionName);
        Assert.Equal("FullySatisfied", storedEntity.Status);
        Assert.Equal("Evaluated 3 mandatory conditions.", storedEntity.Details);
    }

    [Fact]
    public async Task GetByIdAsync_RetrievesStoredRecord_Successfully()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repository = new PostgresGovernanceAuditRepository(dbContext);
        var record = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "EvaluateCompliance",
            "Compliant",
            "Evaluated lease duration compliance.");

        await repository.AddAsync(record);

        // Act
        var retrievedRecord = await repository.GetByIdAsync(record.Id);

        // Assert
        Assert.NotNull(retrievedRecord);
        Assert.Equal(record.Id, retrievedRecord.Id);
        Assert.Equal(EngineType.RegulatoryCompliance, retrievedRecord.EngineType);
        Assert.Equal("EvaluateCompliance", retrievedRecord.ActionName);
        Assert.Equal("Compliant", retrievedRecord.Status);
        Assert.Equal("Evaluated lease duration compliance.", retrievedRecord.Details);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repository = new PostgresGovernanceAuditRepository(dbContext);

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_NullRecord_ThrowsArgumentNullException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repository = new PostgresGovernanceAuditRepository(dbContext);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!));
    }

    [Fact]
    public async Task AddAsync_CancellationTokenPropagation_HonorsCancellation()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repository = new PostgresGovernanceAuditRepository(dbContext);
        var record = GovernanceAuditRecord.Create(
            EngineType.GovernanceConflict,
            "DetectConflicts",
            "ConflictsDetected",
            "Evaluated overlap conflicts.");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AddAsync(record, cts.Token));
    }

    [Fact]
    public async Task AddAsync_PrivacyEnforcement_StoresOnlyAggregateMetricsAndNoPII()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repository = new PostgresGovernanceAuditRepository(dbContext);
        var safeAggregateDetails = "TotalConditions: 5 | MandatoryCount: 3 | SatisfiedMandatoryCount: 3 | Outcome: FullySatisfied";
        var record = GovernanceAuditRecord.Create(
            EngineType.ConditionalVerification,
            "VerifyConditions",
            "FullySatisfied",
            safeAggregateDetails);

        // Act
        await repository.AddAsync(record);

        // Assert
        var retrievedRecord = await repository.GetByIdAsync(record.Id);
        Assert.NotNull(retrievedRecord);

        // Verify absence of sensitive raw PII / document data
        Assert.DoesNotContain("Officer", retrievedRecord.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NIC", retrievedRecord.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Passport", retrievedRecord.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remark", retrievedRecord.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Comment", retrievedRecord.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TotalConditions: 5", retrievedRecord.Details);
    }
}
