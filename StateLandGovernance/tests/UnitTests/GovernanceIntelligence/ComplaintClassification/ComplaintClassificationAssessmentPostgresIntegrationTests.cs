using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.ComplaintClassification;

[Trait("Category", "PostgresIntegration")]
public sealed class ComplaintClassificationAssessmentPostgresIntegrationTests
{
    private const string EnvironmentVariableName = "COMPONENT4_TEST_POSTGRES_CONNECTION";
    private const string ExpectedDatabasePrefix = "component4_screening_test_";
    private const string TargetMigration = "20261008062258_GovernanceIntelligence_AddComplaintClassificationAssessments";
    private const double StoragePrecisionTolerance = 1e-16;

    public static TheoryData<decimal, decimal, decimal, decimal> InvalidDatabaseProbabilities => new()
    {
        { -0.1m, 0.4m, 0.4m, 0.3m },
        { 0.2m, 0.2m, 0.2m, 0.2m }
    };

    [Fact]
    public async Task MigrationChain_AppliesComplaintAssessmentMigration()
    {
        var connectionString = GetValidatedTestConnectionString();

        await using var context = CreateDbContext(connectionString);
        await context.Database.MigrateAsync();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains(TargetMigration, appliedMigrations);
    }

    [Fact]
    public async Task SaveAndReloadAssessment_RoundTripsEveryPersistedField()
    {
        var connectionString = GetValidatedTestConnectionString();
        await using (var migrationContext = CreateDbContext(connectionString))
        {
            await migrationContext.Database.MigrateAsync();
        }

        var firstProbability = 0.12345678901234566;
        var secondProbability = 0.23456789012345677;
        var thirdProbability = 0.34567890123456789;
        var assessment = CreateAssessment(
            Guid.NewGuid(),
            "CASE-C4-PG-001",
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [ComplaintClassificationCategories.AdministrativeProceduralIntegrity] = firstProbability,
                [ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement] = secondProbability,
                [ComplaintClassificationCategories.UnauthorizedAllocationTransferUse] = thirdProbability,
                [ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse] =
                    1.0 - firstProbability - secondProbability - thirdProbability
            });

        try
        {
            await using (var writeContext = CreateDbContext(connectionString))
            {
                var store = new PostgresComplaintClassificationAssessmentStore(writeContext);
                await store.AddAsync(assessment);
            }

            await using var readContext = CreateDbContext(connectionString);
            var readStore = new PostgresComplaintClassificationAssessmentStore(readContext);
            var loaded = await readStore.GetByIdAsync(assessment.AssessmentId);

            Assert.NotNull(loaded);
            Assert.Equal(assessment.AssessmentId, loaded.AssessmentId);
            Assert.Equal(assessment.CaseId, loaded.CaseId);
            Assert.Equal(assessment.ComplaintText, loaded.ComplaintText);
            Assert.Equal(assessment.ModelVersion, loaded.ModelVersion);
            Assert.Equal(assessment.PredictedCategory, loaded.PredictedCategory);
            Assert.Equal(assessment.AdvisoryNote, loaded.AdvisoryNote);
            Assert.Equal(assessment.ClosedSetNote, loaded.ClosedSetNote);
            Assert.Equal(assessment.AssessedAtUtc, loaded.AssessedAtUtc);
            Assert.Equal(ComplaintClassificationCategories.All.Count, loaded.ClassProbabilities.Count);
            foreach (var category in ComplaintClassificationCategories.All)
            {
                Assert.InRange(
                    Math.Abs(assessment.ClassProbabilities[category] - loaded.ClassProbabilities[category]),
                    0,
                    StoragePrecisionTolerance);
            }
        }
        finally
        {
            await using var cleanupContext = CreateDbContext(connectionString);
            await cleanupContext.ComplaintClassificationAssessments
                .Where(item => item.AssessmentId == assessment.AssessmentId)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task SaveAndReloadAssessment_ZeroAndOneProbabilityBoundariesAreAccepted()
    {
        var connectionString = GetValidatedTestConnectionString();
        await ApplyMigrationsAsync(connectionString);
        var assessment = CreateAssessment(
            Guid.NewGuid(),
            "CASE-C4-PG-BOUNDARY",
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [ComplaintClassificationCategories.AdministrativeProceduralIntegrity] = 1.0,
                [ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement] = 0.0,
                [ComplaintClassificationCategories.UnauthorizedAllocationTransferUse] = 0.0,
                [ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse] = 0.0
            });

        try
        {
            await using (var writeContext = CreateDbContext(connectionString))
            {
                await new PostgresComplaintClassificationAssessmentStore(writeContext).AddAsync(assessment);
            }

            await using var readContext = CreateDbContext(connectionString);
            var loaded = await new PostgresComplaintClassificationAssessmentStore(readContext)
                .GetByIdAsync(assessment.AssessmentId);

            Assert.NotNull(loaded);
            Assert.Equal(1.0, loaded.ClassProbabilities[ComplaintClassificationCategories.AdministrativeProceduralIntegrity]);
            Assert.Equal(0.0, loaded.ClassProbabilities[ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement]);
            Assert.Equal(0.0, loaded.ClassProbabilities[ComplaintClassificationCategories.UnauthorizedAllocationTransferUse]);
            Assert.Equal(0.0, loaded.ClassProbabilities[ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse]);
        }
        finally
        {
            await DeleteAssessmentsAsync(connectionString, assessment.AssessmentId);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidDatabaseProbabilities))]
    public async Task DatabaseConstraints_InvalidProbabilityRangeOrSum_IsRejected(
        decimal administrativeProbability,
        decimal revenueProbability,
        decimal unauthorizedUseProbability,
        decimal environmentalProbability)
    {
        var connectionString = GetValidatedTestConnectionString();
        await ApplyMigrationsAsync(connectionString);
        var assessmentId = Guid.NewGuid();
        var entity = CreateEntity(
            assessmentId,
            "CASE-C4-PG-INVALID",
            administrativeProbability,
            revenueProbability,
            unauthorizedUseProbability,
            environmentalProbability);

        try
        {
            await using var context = CreateDbContext(connectionString);
            context.ComplaintClassificationAssessments.Add(entity);

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, postgresException.SqlState);
        }
        finally
        {
            await DeleteAssessmentsAsync(connectionString, assessmentId);
        }
    }

    [Fact]
    public async Task SaveAssessments_SameCaseId_PreservesDistinctHistoricalRows()
    {
        var connectionString = GetValidatedTestConnectionString();
        await ApplyMigrationsAsync(connectionString);
        var first = CreateAssessment(Guid.NewGuid(), "CASE-C4-PG-HISTORY", ValidSimpleProbabilities());
        var second = CreateAssessment(Guid.NewGuid(), "CASE-C4-PG-HISTORY", ValidSimpleProbabilities());

        try
        {
            await using (var firstContext = CreateDbContext(connectionString))
            {
                await new PostgresComplaintClassificationAssessmentStore(firstContext).AddAsync(first);
            }

            await using (var secondContext = CreateDbContext(connectionString))
            {
                await new PostgresComplaintClassificationAssessmentStore(secondContext).AddAsync(second);
            }

            await using var readContext = CreateDbContext(connectionString);
            var rows = await readContext.ComplaintClassificationAssessments
                .AsNoTracking()
                .Where(item => item.CaseId == first.CaseId)
                .OrderBy(item => item.AssessmentId)
                .ToListAsync();

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, item => item.AssessmentId == first.AssessmentId);
            Assert.Contains(rows, item => item.AssessmentId == second.AssessmentId);
        }
        finally
        {
            await DeleteAssessmentsAsync(connectionString, first.AssessmentId, second.AssessmentId);
        }
    }

    private static ComplaintClassificationAssessment CreateAssessment(
        Guid assessmentId,
        string caseId,
        IReadOnlyDictionary<string, double> probabilities) =>
        new(
            assessmentId,
            caseId,
            "A fictional complaint used only for PostgreSQL verification.",
            "governance_classifier_v1",
            ComplaintClassificationCategories.UnauthorizedAllocationTransferUse,
            probabilities,
            "Advisory output only; human review is required.",
            "The model selects from a closed four-category taxonomy.",
            new DateTimeOffset(2026, 10, 8, 7, 0, 0, TimeSpan.Zero));

    private static IReadOnlyDictionary<string, double> ValidSimpleProbabilities() =>
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            [ComplaintClassificationCategories.AdministrativeProceduralIntegrity] = 0.1,
            [ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement] = 0.2,
            [ComplaintClassificationCategories.UnauthorizedAllocationTransferUse] = 0.6,
            [ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse] = 0.1
        };

    private static ComplaintClassificationAssessmentEntity CreateEntity(
        Guid assessmentId,
        string caseId,
        decimal administrativeProbability,
        decimal revenueProbability,
        decimal unauthorizedUseProbability,
        decimal environmentalProbability) =>
        new()
        {
            AssessmentId = assessmentId,
            CaseId = caseId,
            ComplaintText = "A fictional invalid-probability database constraint test.",
            ModelVersion = "governance_classifier_v1",
            PredictedCategory = ComplaintClassificationCategories.AdministrativeProceduralIntegrity,
            AdministrativeProceduralIntegrityProbability = administrativeProbability,
            LeaseRevenuePaymentEnforcementProbability = revenueProbability,
            UnauthorizedAllocationTransferUseProbability = unauthorizedUseProbability,
            ProtectedEnvironmentalLeaseMisuseProbability = environmentalProbability,
            AdvisoryNote = "Advisory output only.",
            ClosedSetNote = "Closed-set taxonomy.",
            AssessedAtUtc = new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero)
        };

    private static async Task ApplyMigrationsAsync(string connectionString)
    {
        await using var context = CreateDbContext(connectionString);
        await context.Database.MigrateAsync();
    }

    private static async Task DeleteAssessmentsAsync(string connectionString, params Guid[] assessmentIds)
    {
        await using var context = CreateDbContext(connectionString);
        await context.ComplaintClassificationAssessments
            .Where(item => assessmentIds.Contains(item.AssessmentId))
            .ExecuteDeleteAsync();
    }

    private static string GetValidatedTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing test configuration: Environment variable '{EnvironmentVariableName}' must reference a dedicated PostgreSQL test database with name prefix '{ExpectedDatabasePrefix}'.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !builder.Database.StartsWith(ExpectedDatabasePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Safety check failed: the configured database name must start with '{ExpectedDatabasePrefix}'.");
        }

        return connectionString;
    }

    private static GovernanceIntelligenceDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(GovernanceIntelligenceDbContext).Assembly.GetName().Name);
                npgsqlOptions.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    GovernanceIntelligenceDbContext.SchemaName);
            })
            .Options;

        return new GovernanceIntelligenceDbContext(options);
    }
}
