using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

[Trait("Category", "PostgresIntegration")]
public class EarlyGovernanceScreeningPostgresIntegrationTests
{
    private const string PrecedingMigration = "20260825141654_GovernanceIntelligence_RemoveLegacyComplianceChildTables";
    private const string TargetMigration = "20260918072800_GovernanceIntelligence_AddEarlyGovernanceScreeningEvaluations";
    private const string EnvironmentVariableName = "COMPONENT4_TEST_POSTGRES_CONNECTION";
    private const string ExpectedDatabasePrefix = "component4_screening_test_";

    private static readonly DateTimeOffset SampleUtcTime = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static string GetValidatedTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing test configuration: Environment variable '{EnvironmentVariableName}' is not set. " +
                "To run PostgreSQL integration tests, set this variable to a connection string for a dedicated test database " +
                $"with name prefix '{ExpectedDatabasePrefix}'.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !builder.Database.StartsWith(ExpectedDatabasePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Safety check failed: The configured test database name '{builder.Database}' does not start with the " +
                $"mandatory test prefix '{ExpectedDatabasePrefix}'. Aborting to prevent modifications to working development databases.");
        }

        return connectionString;
    }

    private static GovernanceIntelligenceDbContext CreateDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(GovernanceIntelligenceDbContext).Assembly.GetName().Name);
            npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", GovernanceIntelligenceDbContext.SchemaName);
        });

        return new GovernanceIntelligenceDbContext(optionsBuilder.Options);
    }

    private static EarlyGovernanceScreeningResultDto CreateSampleResult(
        string caseId = "CASE-PG-2026-001",
        string inputVersion = "v1.0")
    {
        var engine = new EarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(engine);

        var command = new ScreenEarlyGovernanceCommand(
            CaseId: caseId,
            InputVersion: inputVersion,
            Indicators: new List<EarlyGovernanceIndicatorDto>
            {
                new("LegalDispute", "VerifiedPresent", "DOC-DISPUTE-01", SampleUtcTime),
                new("UnauthorizedOccupation", "Unavailable"),
                new("UnauthorizedConstruction", "VerifiedAbsent", "DOC-SURVEY-01", SampleUtcTime),
                new("FamilyOrInheritanceClaim", "Unverified"),
                new("MultipleClaimants", "NotApplicable", "DOC-NA-01", SampleUtcTime),
                new("UnresolvedObjection", "Missing"),
                new("PreviousIllegalLandActivity", "VerifiedAbsent", "DOC-POLICE-01", SampleUtcTime)
            }
        );

        return handler.HandleAsync(command).GetAwaiter().GetResult();
    }

    [Fact]
    public void SafetyGuard_MissingEnvironmentVariable_ThrowsClearSanitizedMessage()
    {
        var currentVal = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);
            var ex = Assert.Throws<InvalidOperationException>(() => GetValidatedTestConnectionString());
            Assert.Contains("Missing test configuration", ex.Message);
            Assert.Contains(EnvironmentVariableName, ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, currentVal);
        }
    }

    [Fact]
    public void SafetyGuard_UnsafeDatabaseName_ThrowsSafetyCheckMessage()
    {
        var currentVal = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, "Host=localhost;Database=state_land_governance;Username=test;Password=test");
            var ex = Assert.Throws<InvalidOperationException>(() => GetValidatedTestConnectionString());
            Assert.Contains("Safety check failed", ex.Message);
            Assert.Contains(ExpectedDatabasePrefix, ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, currentVal);
        }
    }

    [Fact]
    public async Task RunCompletePostgresIntegrationVerificationAsync()
    {
        var connectionString = GetValidatedTestConnectionString();

        // -------------------------------------------------------------
        // Step 1: Apply migrations through preceding migration & verify table is absent
        // -------------------------------------------------------------
        using (var initialContext = CreateDbContext(connectionString))
        {
            var migrator = initialContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PrecedingMigration);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var tableExistsBefore = await TableExistsAsync(conn, "governance_intelligence", "early_governance_screening_evaluations");
            Assert.False(tableExistsBefore, "Target table must be absent prior to applying the new migration.");
        }

        // -------------------------------------------------------------
        // Step 2: Apply the new early-governance migration & verify presence
        // -------------------------------------------------------------
        using (var migrationContext = CreateDbContext(connectionString))
        {
            var migrator = migrationContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(TargetMigration);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var tableExistsAfter = await TableExistsAsync(conn, "governance_intelligence", "early_governance_screening_evaluations");
            Assert.True(tableExistsAfter, "Target table must exist after applying the new migration.");

            var historyExists = await MigrationHistoryEntryExistsAsync(conn, TargetMigration);
            Assert.True(historyExists, $"Migration history entry for '{TargetMigration}' must be present.");
        }

        // -------------------------------------------------------------
        // Step 3: Verify PostgreSQL database schema structure
        // -------------------------------------------------------------
        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            // Columns and types
            var columns = await GetColumnsMetadataAsync(conn, "governance_intelligence", "early_governance_screening_evaluations");

            Assert.Equal("uuid", columns["assessment_id"].DataType);
            Assert.Equal("NO", columns["assessment_id"].IsNullable);

            Assert.Equal("character varying", columns["case_id"].DataType);
            Assert.Equal(100, columns["case_id"].MaxLength);
            Assert.Equal("NO", columns["case_id"].IsNullable);

            Assert.Equal("character varying", columns["input_version"].DataType);
            Assert.Equal(50, columns["input_version"].MaxLength);
            Assert.Equal("NO", columns["input_version"].IsNullable);

            Assert.Equal("timestamp with time zone", columns["created_at_utc"].DataType);
            Assert.Equal("NO", columns["created_at_utc"].IsNullable);

            Assert.Equal("integer", columns["snapshot_schema_version"].DataType);
            Assert.Equal("NO", columns["snapshot_schema_version"].IsNullable);

            Assert.Equal("jsonb", columns["result_snapshot_json"].DataType);
            Assert.Equal("NO", columns["result_snapshot_json"].IsNullable);

            // Primary key
            var pkColumns = await GetPrimaryKeyColumnsAsync(conn, "governance_intelligence", "early_governance_screening_evaluations");
            Assert.Single(pkColumns);
            Assert.Equal("assessment_id", pkColumns[0]);

            // Unique constraints (confirm NO unique constraint on case_id + input_version)
            var uniqueConstraints = await GetUniqueConstraintsAsync(conn, "governance_intelligence", "early_governance_screening_evaluations");
            Assert.Empty(uniqueConstraints);
        }

        // -------------------------------------------------------------
        // Step 4: Storage Scenario A — Save and reload
        // -------------------------------------------------------------
        var assessmentId1 = Guid.NewGuid();
        var sampleResult1 = CreateSampleResult("CASE-PG-001", "v1.0");

        using (var writeContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(writeContext);
            await store.AddAsync(assessmentId1, SampleUtcTime, sampleResult1);
        }

        using (var readContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var retrieved = await store.GetByIdAsync(assessmentId1);

            Assert.NotNull(retrieved);
            Assert.Equal(assessmentId1, retrieved.AssessmentId);
            Assert.Equal(SampleUtcTime, retrieved.CreatedAtUtc);
            Assert.Equal(1, retrieved.SnapshotSchemaVersion);

            Assert.Equal(sampleResult1.CaseId, retrieved.Result.CaseId);
            Assert.Equal(sampleResult1.InputVersion, retrieved.Result.InputVersion);
            Assert.Equal(sampleResult1.OverallStatus, retrieved.Result.OverallStatus);
            Assert.Equal(sampleResult1.HasIncompleteEvidence, retrieved.Result.HasIncompleteEvidence);
            Assert.Equal(sampleResult1.IndicatorResults.Count, retrieved.Result.IndicatorResults.Count);

            for (int i = 0; i < sampleResult1.IndicatorResults.Count; i++)
            {
                var expected = sampleResult1.IndicatorResults[i];
                var actual = retrieved.Result.IndicatorResults[i];

                Assert.Equal(expected.IndicatorType, actual.IndicatorType);
                Assert.Equal(expected.EvidenceState, actual.EvidenceState);
                Assert.Equal(expected.RequiresReview, actual.RequiresReview);
                Assert.Equal(expected.NeedsEvidence, actual.NeedsEvidence);
                Assert.Equal(expected.ReasonCode, actual.ReasonCode);
                Assert.Equal(expected.Message, actual.Message);
                Assert.Equal(expected.EvidenceReference, actual.EvidenceReference);
                Assert.Equal(expected.RecordedAtUtc, actual.RecordedAtUtc);
            }
        }

        // -------------------------------------------------------------
        // Step 5: Storage Scenario B — Multiple assessments for same CaseId and InputVersion
        // -------------------------------------------------------------
        var assessmentId2 = Guid.NewGuid();
        var sampleUtcTime2 = SampleUtcTime.AddHours(3);

        using (var writeContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(writeContext);
            await store.AddAsync(assessmentId2, sampleUtcTime2, sampleResult1);
        }

        using (var readContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);

            var read1 = await store.GetByIdAsync(assessmentId1);
            var read2 = await store.GetByIdAsync(assessmentId2);

            Assert.NotNull(read1);
            Assert.NotNull(read2);
            Assert.Equal(assessmentId1, read1.AssessmentId);
            Assert.Equal(assessmentId2, read2.AssessmentId);
            Assert.Equal(SampleUtcTime, read1.CreatedAtUtc);
            Assert.Equal(sampleUtcTime2, read2.CreatedAtUtc);
            Assert.Equal(sampleResult1.CaseId, read1.Result.CaseId);
            Assert.Equal(sampleResult1.CaseId, read2.Result.CaseId);
        }

        // -------------------------------------------------------------
        // Step 6: Storage Scenario C — Duplicate assessment ID triggers 23505 unique violation
        // -------------------------------------------------------------
        using (var failContext = CreateDbContext(connectionString))
        {
            var failStore = new PostgresEarlyGovernanceScreeningStore(failContext);

            var dbEx = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await failStore.AddAsync(assessmentId1, SampleUtcTime.AddDays(1), sampleResult1);
            });

            var postgresEx = Assert.IsType<PostgresException>(dbEx.InnerException);
            Assert.Equal("23505", postgresEx.SqlState);
        }

        using (var verifyContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(verifyContext);
            var original = await store.GetByIdAsync(assessmentId1);

            Assert.NotNull(original);
            Assert.Equal(SampleUtcTime, original.CreatedAtUtc);
        }

        // -------------------------------------------------------------
        // Step 7: Storage Scenario D — Unknown ID returns null
        // -------------------------------------------------------------
        using (var readContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var result = await store.GetByIdAsync(Guid.NewGuid());
            Assert.Null(result);
        }

        // -------------------------------------------------------------
        // Step 8: Storage Scenario E — Cancellation propagation
        // -------------------------------------------------------------
        var cancelledAssessmentId = Guid.NewGuid();
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();

            using var cancelContext = CreateDbContext(connectionString);
            var store = new PostgresEarlyGovernanceScreeningStore(cancelContext);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                store.AddAsync(cancelledAssessmentId, SampleUtcTime, sampleResult1, cts.Token));
        }

        using (var verifyContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(verifyContext);
            var retrieved = await store.GetByIdAsync(cancelledAssessmentId);
            Assert.Null(retrieved);
        }

        // -------------------------------------------------------------
        // Step 9: Storage Scenario F — Structurally incomplete snapshot via raw SQL
        // -------------------------------------------------------------
        var incompleteAssessmentId = Guid.NewGuid();
        const string secretMarker = "CONFIDENTIAL_INCOMPLETE_MARKER_987654";
        var incompleteJson = $$"""
        {
            "caseId": "CASE-INCOMPLETE-PG",
            "inputVersion": "v1.0",
            "hasIncompleteEvidence": false,
            "secretPayload": "{{secretMarker}}",
            "indicatorResults": []
        }
        """; // overallStatus is deliberately omitted

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO governance_intelligence.early_governance_screening_evaluations
                (assessment_id, case_id, input_version, created_at_utc, snapshot_schema_version, result_snapshot_json)
                VALUES (@id, @caseId, @inputVersion, @createdAt, @schemaVersion, @json::jsonb);
            """;
            cmd.Parameters.AddWithValue("id", incompleteAssessmentId);
            cmd.Parameters.AddWithValue("caseId", "CASE-INCOMPLETE-PG");
            cmd.Parameters.AddWithValue("inputVersion", "v1.0");
            cmd.Parameters.AddWithValue("createdAt", SampleUtcTime);
            cmd.Parameters.AddWithValue("schemaVersion", 1);
            cmd.Parameters.AddWithValue("json", incompleteJson);
            await cmd.ExecuteNonQueryAsync();
        }

        using (var readContext = CreateDbContext(connectionString))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(incompleteAssessmentId));

            Assert.Contains("overallStatus", ex.Message);
            Assert.DoesNotContain(secretMarker, ex.Message);
            Assert.DoesNotContain(secretMarker, ex.ToString());
        }
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string schema, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @schema AND table_name = @table
            );
        """;
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<bool> MigrationHistoryEntryExistsAsync(NpgsqlConnection conn, string migrationId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM governance_intelligence.__ef_migrations_history
                WHERE "MigrationId" = @migrationId
            );
        """;
        cmd.Parameters.AddWithValue("migrationId", migrationId);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private record ColumnMetadata(string DataType, int? MaxLength, string IsNullable);

    private static async Task<Dictionary<string, ColumnMetadata>> GetColumnsMetadataAsync(
        NpgsqlConnection conn, string schema, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type, character_maximum_length, is_nullable
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table;
        """;
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);

        var result = new Dictionary<string, ColumnMetadata>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var maxLen = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            var isNullable = reader.GetString(3);
            result[colName] = new ColumnMetadata(dataType, maxLen, isNullable);
        }

        return result;
    }

    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        NpgsqlConnection conn, string schema, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name
              AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = @schema
              AND tc.table_name = @table
              AND tc.constraint_type = 'PRIMARY KEY'
            ORDER BY kcu.ordinal_position;
        """;
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<string>> GetUniqueConstraintsAsync(
        NpgsqlConnection conn, string schema, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tc.constraint_name
            FROM information_schema.table_constraints tc
            WHERE tc.table_schema = @schema
              AND tc.table_name = @table
              AND tc.constraint_type = 'UNIQUE';
        """;
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);

        var constraints = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            constraints.Add(reader.GetString(0));
        }

        return constraints;
    }
}
