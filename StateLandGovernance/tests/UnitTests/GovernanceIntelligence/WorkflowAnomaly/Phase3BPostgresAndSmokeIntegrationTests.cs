using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.WorkflowAnomaly;

/// <summary>
/// Phase 3B real PostgreSQL migration, schema, repository, constraint, and end-to-end FastAPI anomaly orchestration verification.
/// Targets an isolated test database (<c>component4_screening_test_*</c>) configured via <c>COMPONENT4_TEST_POSTGRES_CONNECTION</c>.
/// </summary>
public sealed class Phase3BPostgresAndSmokeIntegrationTests
{
    private const string ConnectionEnvVar = "COMPONENT4_TEST_POSTGRES_CONNECTION";
    private const string AllowedTestDatabasePrefix = "component4_screening_test_";
    private const string TargetPhase3AMigrationId = "20261011193437_GovernanceIntelligence_AddPhase3AProjectionsAndAssessments";
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;

    public Phase3BPostgresAndSmokeIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed record SyntheticTracePayload(
        string case_id,
        List<SyntheticEventPayload> events);

    private sealed record SyntheticEventPayload(
        int event_seq,
        string activity,
        string institution,
        string resource,
        string timestamp);

    [Fact]
    public async Task Postgres_MigrationsSchemaConstraintsAndAllFourRepositories_VerifyEndToEnd()
    {
        var connectionString = GetValidatedTestDatabaseConnectionString();
        await using var dbContext = CreatePostgresDbContext(connectionString);

        // 1. Apply full migration chain through 20261011193437_GovernanceIntelligence_AddPhase3AProjectionsAndAssessments
        await dbContext.Database.MigrateAsync();

        await using var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        // Log PostgreSQL version and applied migrations for verification report
        await using (var versionCmd = new NpgsqlCommand("SELECT version();", conn))
        {
            var pgVersion = (string?)await versionCmd.ExecuteScalarAsync();
            _output.WriteLine($"PostgreSQL Version: {pgVersion}");
            Assert.False(string.IsNullOrWhiteSpace(pgVersion));
        }

        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
        _output.WriteLine($"Applied Migrations ({appliedMigrations.Count}): {string.Join(", ", appliedMigrations)}");
        Assert.Contains(TargetPhase3AMigrationId, appliedMigrations);

        // 2. Verify PostgreSQL catalog metadata: tables, column types, unique indexes, lookup indexes, check constraints
        await VerifyPostgresCatalogSchemaAsync(conn);

        // 3. Exercise Component 1 Case Projection Store against real PostgreSQL
        await VerifyComponent1CaseProjectionStorePostgresAsync(connectionString);

        // 4. Exercise Component 3 Workflow Projection Store against real PostgreSQL (including JSONB verbatim naive timestamp round-trip)
        await VerifyComponent3WorkflowProjectionStorePostgresAsync(connectionString);

        // 5. Exercise Workflow Anomaly Assessment Store against real PostgreSQL (including concurrent upsert race recovery & check constraints)
        await VerifyWorkflowAnomalyAssessmentStorePostgresAsync(connectionString);

        // 6. Exercise Lease Approval Timing Assessment Store against real PostgreSQL
        await VerifyLeaseApprovalTimingAssessmentStorePostgresAsync(connectionString);
    }

    [Fact]
    public async Task RealOrchestrationSmoke_DotNetHandlerToFastApiToPostgresAndQuery_VerifiesDirectAndProjectedTraces()
    {
        var connectionString = GetValidatedTestDatabaseConnectionString();
        await using var setupContext = CreatePostgresDbContext(connectionString);
        await setupContext.Database.MigrateAsync();

        // 1. Locate frozen ML bundle and synthetic trace fixture
        var repoRoot = FindRepositoryRoot();
        var mlDir = Directory.Exists(Path.Combine(repoRoot, "src", "Modules", "GovernanceIntelligence", "ML"))
            ? Path.Combine(repoRoot, "src", "Modules", "GovernanceIntelligence", "ML")
            : Path.Combine(repoRoot, "StateLandGovernance", "src", "Modules", "GovernanceIntelligence", "ML");
        var mlSrcDir = Path.Combine(mlDir, "src");
        var exampleTraceFile = Path.Combine(mlDir, "anomaly_detection", "examples", "complete_synthetic_trace.json");
        var modelBundleConfigFile = Path.Combine(mlDir, "anomaly_detection", "bundles", "workflow_anomaly_model_v1", "local_inference_config.json");

        Assert.True(File.Exists(exampleTraceFile), $"Synthetic trace fixture not found: {exampleTraceFile}");
        Assert.True(File.Exists(modelBundleConfigFile), $"Frozen model bundle config not found: {modelBundleConfigFile}");

        int port = GetAvailablePort();
        var baseAddress = $"http://127.0.0.1:{port}";

        var configuredPython = Environment.GetEnvironmentVariable("COMPONENT4_TEST_PYTHON_EXECUTABLE");
        var usePythonLauncher = string.IsNullOrWhiteSpace(configuredPython);
        var startInfo = new ProcessStartInfo
        {
            FileName = usePythonLauncher ? "py" : configuredPython!,
            Arguments = $"{(usePythonLauncher ? "-3.13 " : string.Empty)}-m uvicorn http_service:app --app-dir \"{mlSrcDir}\" --host 127.0.0.1 --port {port}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["PYTHONPATH"] = $"{mlDir};{mlSrcDir}";
        startInfo.Environment["GOVERNANCE_MODEL_VERSION"] = "v2";

        using var process = new Process { StartInfo = startInfo };
        var outputLog = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outputLog.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) outputLog.AppendLine(e.Data); };

        Guid? createdProjectionId = null;
        var createdAssessmentIds = new List<Guid>();

        try
        {
            Assert.True(process.Start(), "Failed to start Python FastAPI uvicorn process.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            bool isReady = false;
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"FastAPI process exited prematurely with code {process.ExitCode}. Log:\n{outputLog}");
                }

                try
                {
                    var response = await healthClient.GetAsync($"{baseAddress}/health");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        isReady = true;
                        break;
                    }
                }
                catch
                {
                    // Wait for uvicorn bind
                }

                await Task.Delay(250);
            }

            Assert.True(isReady, $"FastAPI service did not become healthy within 20 seconds. Log:\n{outputLog}");

            var traceJson = await File.ReadAllTextAsync(exampleTraceFile);
            var parsedPayload = JsonSerializer.Deserialize<SyntheticTracePayload>(traceJson)!;
            Assert.NotNull(parsedPayload);

            var domainEvents = parsedPayload.events
                .Select(e => new WorkflowAnomalyEvent(e.event_seq, e.activity, e.institution, e.resource, e.timestamp))
                .ToList();

            // Direct HTTP reference call to FastAPI /workflow-anomaly/predict
            using var directClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var directPost = new StringContent(traceJson, Encoding.UTF8, "application/json");
            var directHttpResponse = await directClient.PostAsync($"{baseAddress}/workflow-anomaly/predict", directPost);
            Assert.Equal(HttpStatusCode.OK, directHttpResponse.StatusCode);
            var directJsonText = await directHttpResponse.Content.ReadAsStringAsync();
            using var directDocument = JsonDocument.Parse(directJsonText);
            var directRoot = directDocument.RootElement;

            double expectedScore = directRoot.GetProperty("anomaly_score").GetDouble();
            double expectedThreshold = directRoot.GetProperty("threshold").GetDouble();
            bool expectedFlagged = directRoot.GetProperty("flagged").GetBoolean();
            string expectedModelVersion = directRoot.GetProperty("model_version").GetString()!;
            string expectedAdvisoryNote = directRoot.GetProperty("advisory_note").GetString()!;

            // Build DI container with real HttpWorkflowAnomalyClient + real PostgreSQL repositories + handlers
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedUtcNow));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:GovernanceIntelligenceConnection"] = connectionString,
                    ["WorkflowAnomaly:BaseUrl"] = baseAddress,
                    ["WorkflowAnomaly:TimeoutSeconds"] = "10"
                })
                .Build();

            services.AddGovernanceIntelligenceInfrastructure(config, new FakeHostEnvironment());
            await using var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();
            var projectionStore = scope.ServiceProvider.GetRequiredService<IComponent3WorkflowGovernanceProjectionStore>();
            var commandHandler = scope.ServiceProvider.GetRequiredService<AssessWorkflowAnomalyCommandHandler>();

            var workflowRunId = Guid.NewGuid();
            var provenance = GovernanceInputProvenance.CreatePp1Fixture(
                sourceSystem: "Component3.SyntheticFixture",
                sourceRecordId: workflowRunId.ToString("D"),
                sourceRecordVersion: "v1",
                sourceObservedAtUtc: FixedUtcNow,
                syntheticSeedId: "PP1-SMOKE-C3-01",
                generatedAtUtc: FixedUtcNow);

            // A) Persist Component 3 workflow projection containing the exact synthetic trace events + naive timestamps
            var colomboOffset = TimeSpan.FromHours(5.5);
            var projectionEvents = parsedPayload.events.Select((e, idx) =>
            {
                var naiveDt = DateTime.ParseExact(e.timestamp, "yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                return new Component3WorkflowProjectionEvent(
                    eventId: $"SMOKE-EVT-{e.event_seq}",
                    eventSeq: e.event_seq,
                    activity: e.activity,
                    stageCode: $"STAGE_{e.event_seq:00}",
                    actorRole: "SyntheticOfficer",
                    institutionCode: e.institution,
                    timestamp: new DateTimeOffset(naiveDt, colomboOffset),
                    eventOutcome: idx == parsedPayload.events.Count - 1 ? "Approved" : "Completed",
                    evidenceRef: $"SMOKE-REF-{e.event_seq}",
                    resource: e.resource,
                    anomalyInputTimestamp: e.timestamp);
            }).ToList();

            var startBoundary = new TimingBoundaryEvidence(
                projectionEvents[0].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "Component3.Smoke", "SMOKE-EVT-1", parsedPayload.case_id, workflowRunId);
            var handoverBoundary = new TimingBoundaryEvidence(
                projectionEvents[^1].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "Component3.Smoke", $"SMOKE-EVT-{projectionEvents.Count}", parsedPayload.case_id, workflowRunId);

            var projSha = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
                workflowRunId,
                parsedPayload.case_id,
                "StateLandLeaseApproval",
                Component3WorkflowTraceStatus.Completed,
                Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
                $"STAGE_{projectionEvents.Count:00}",
                Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
                "Asia/Colombo",
                projectionEvents,
                startBoundary,
                handoverBoundary,
                provenance);

            var c3Projection = new Component3WorkflowGovernanceProjection(
                Guid.NewGuid(),
                workflowRunId,
                parsedPayload.case_id,
                "StateLandLeaseApproval",
                Component3WorkflowTraceStatus.Completed,
                Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
                $"STAGE_{projectionEvents.Count:00}",
                Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
                "Asia/Colombo",
                projectionEvents,
                startBoundary,
                handoverBoundary,
                projSha,
                provenance,
                FixedUtcNow);

            var persistedProjection = await projectionStore.UpsertAsync(c3Projection);
            createdProjectionId = persistedProjection.ProjectionId;

            // B) Execute orchestration from stored PostgreSQL Component 3 projection -> FastAPI -> PostgreSQL assessment store
            var projectedResult = await commandHandler.HandleAsync(
                AssessWorkflowAnomalyCommand.FromProjection(
                    persistedProjection.ProjectionId,
                    parsedPayload.case_id,
                    workflowRunId));

            Assert.True(projectedResult.IsSuccess, projectedResult.Error?.Message);
            var assessment = Assert.IsType<WorkflowAnomalyAssessment>(projectedResult.Assessment);
            createdAssessmentIds.Add(assessment.AssessmentId);

            // C) Execute orchestration with direct trace for the same WorkflowRunId -> must be idempotent with identical InputSha256 and AssessmentId
            var directTraceResult = await commandHandler.HandleAsync(
                AssessWorkflowAnomalyCommand.FromTrace(
                    parsedPayload.case_id,
                    workflowRunId,
                    domainEvents,
                    provenance));

            Assert.True(directTraceResult.IsSuccess, directTraceResult.Error?.Message);
            Assert.Equal(assessment.InputSha256, directTraceResult.Assessment!.InputSha256);
            Assert.Equal(assessment.AssessmentId, directTraceResult.Assessment.AssessmentId);

            // D) Retrieve via Query Handlers from fresh PostgreSQL DbContext scope and verify every field matches FastAPI response
            using var readScope = provider.CreateScope();
            var readById = readScope.ServiceProvider.GetRequiredService<GetWorkflowAnomalyAssessmentQueryHandler>();
            var readByCase = readScope.ServiceProvider.GetRequiredService<GetWorkflowAnomalyHistoryByCaseQueryHandler>();
            var readByRun = readScope.ServiceProvider.GetRequiredService<GetWorkflowAnomalyHistoryByWorkflowRunQueryHandler>();

            var retrieved = await readById.HandleAsync(new GetWorkflowAnomalyAssessmentQuery(assessment.AssessmentId));
            Assert.NotNull(retrieved);
            Assert.Equal(parsedPayload.case_id, retrieved!.CaseId);
            Assert.Equal(workflowRunId, retrieved.WorkflowRunId);
            Assert.Equal(persistedProjection.ProjectionId, retrieved.WorkflowProjectionId);
            Assert.Equal(expectedModelVersion, retrieved.ModelVersion);
            Assert.Equal(WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1, retrieved.ModelArtifactSha256);
            Assert.Equal(expectedScore, retrieved.AnomalyScore, 6);
            Assert.Equal(expectedThreshold, retrieved.Threshold, 12);
            Assert.Equal(expectedFlagged, retrieved.Flagged);
            Assert.Equal(expectedScore > expectedThreshold, retrieved.Flagged);
            Assert.Equal(expectedAdvisoryNote, retrieved.AdvisoryNote);

            // Verify all 9 Isolation Forest feature values persisted in PostgreSQL
            Assert.Equal(4, retrieved.EventCount);
            Assert.Equal(2.041667, retrieved.ElapsedDays, 5);
            Assert.Equal(23.0, retrieved.MaxGapHours, 5);
            Assert.Equal(16.333333, retrieved.MeanGapHours, 5);
            Assert.Equal(4, retrieved.UniqueActivities);
            Assert.Equal(0, retrieved.RepeatedActivityCount);
            Assert.Equal(2, retrieved.ResourceHandoffs);
            Assert.Equal(2, retrieved.InstitutionSwitches);
            Assert.Equal(3, retrieved.DistinctResources);

            var historyByCase = await readByCase.HandleAsync(new GetWorkflowAnomalyHistoryByCaseQuery(parsedPayload.case_id, 10));
            Assert.Contains(historyByCase, x => x.AssessmentId == assessment.AssessmentId);

            var historyByRun = await readByRun.HandleAsync(new GetWorkflowAnomalyHistoryByWorkflowRunQuery(workflowRunId, 10));
            Assert.Single(historyByRun);
            Assert.Equal(assessment.AssessmentId, historyByRun[0].AssessmentId);

            _output.WriteLine(
                $"Smoke verified: CaseId={retrieved.CaseId}, Score={retrieved.AnomalyScore:F6}, Threshold={retrieved.Threshold:F6}, Flagged={retrieved.Flagged}, InputSha256={retrieved.InputSha256}");
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
                catch
                {
                    // Best effort termination
                }
            }

            // Clean up test rows created in the isolated test database
            await using var cleanupContext = CreatePostgresDbContext(connectionString);
            if (createdAssessmentIds.Count > 0)
            {
                var rows = await cleanupContext.WorkflowAnomalyAssessments
                    .Where(x => createdAssessmentIds.Contains(x.AssessmentId))
                    .ToListAsync();
                cleanupContext.WorkflowAnomalyAssessments.RemoveRange(rows);
            }

            if (createdProjectionId.HasValue)
            {
                var projRow = await cleanupContext.Component3WorkflowProjections
                    .FirstOrDefaultAsync(x => x.ProjectionId == createdProjectionId.Value);
                if (projRow is not null)
                {
                    cleanupContext.Component3WorkflowProjections.Remove(projRow);
                }
            }

            await cleanupContext.SaveChangesAsync();
        }
    }

    // =========================================================================
    // PostgreSQL Catalog & Table Verification Helpers
    // =========================================================================

    private static async Task VerifyPostgresCatalogSchemaAsync(NpgsqlConnection conn)
    {
        var expectedTables = new[]
        {
            "component1_case_projections",
            "component3_workflow_projections",
            "workflow_anomaly_assessments",
            "lease_approval_timing_assessments"
        };

        foreach (var table in expectedTables)
        {
            await using var tableCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'governance_intelligence' AND table_name = @t;",
                conn);
            tableCmd.Parameters.AddWithValue("t", table);
            var count = (long)(await tableCmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, count);
        }

        // Verify JSONB columns
        var jsonbColumns = new (string Table, string Column)[]
        {
            ("component1_case_projections", "overlapping_sensitive_zone_codes_json"),
            ("component1_case_projections", "parcel_evidence_refs_json"),
            ("component3_workflow_projections", "events_snapshot_json")
        };

        foreach (var (table, col) in jsonbColumns)
        {
            await using var colCmd = new NpgsqlCommand(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'governance_intelligence' AND table_name = @t AND column_name = @c;",
                conn);
            colCmd.Parameters.AddWithValue("t", table);
            colCmd.Parameters.AddWithValue("c", col);
            var dataType = (string?)await colCmd.ExecuteScalarAsync();
            Assert.Equal("jsonb", dataType);
        }

        // Verify expected unique and lookup indexes exist
        var expectedIndexes = new[]
        {
            "ux_c1_case_projections_case_version_sha256",
            "ix_c1_case_projections_case_id",
            "ux_c3_workflow_projections_run_version_sha256",
            "ix_c3_workflow_projections_workflow_run_id",
            "ix_c3_workflow_projections_case_id",
            "ux_workflow_anomaly_assessments_run_model_sha256",
            "ix_workflow_anomaly_assessments_workflow_run_id",
            "ix_workflow_anomaly_assessments_case_id",
            "ux_lease_approval_timing_assessments_run_policy_sha256",
            "ix_lease_approval_timing_assessments_workflow_run_id"
        };

        foreach (var indexName in expectedIndexes)
        {
            await using var idxCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'governance_intelligence' AND indexname = @i;",
                conn);
            idxCmd.Parameters.AddWithValue("i", indexName);
            var count = (long)(await idxCmd.ExecuteScalarAsync() ?? 0L);
            Assert.True(count == 1L, $"Expected index '{indexName}' in schema 'governance_intelligence'.");
        }

        // Verify expected check constraints exist
        var expectedCheckConstraints = new[]
        {
            "ck_c1_case_projections_extent_ha_positive",
            "ck_c1_case_projections_synthetic_consistency",
            "ck_c3_workflow_projections_event_count_positive",
            "ck_c3_workflow_projections_anomaly_eligibility_consistency",
            "ck_c3_workflow_projections_synthetic_consistency",
            "ck_workflow_anomaly_assessments_event_count_positive",
            "ck_workflow_anomaly_assessments_feature_counts_valid",
            "ck_workflow_anomaly_assessments_synthetic_consistency",
            "ck_lease_approval_timing_assessments_time_zone_colombo",
            "ck_lease_approval_timing_assessments_unavailable_consistency",
            "ck_lease_approval_timing_assessments_synthetic_consistency"
        };

        foreach (var constraintName in expectedCheckConstraints)
        {
            await using var ckCmd = new NpgsqlCommand(
                @"SELECT COUNT(*)
                  FROM pg_constraint c
                  JOIN pg_namespace n ON n.oid = c.connamespace
                  WHERE n.nspname = 'governance_intelligence' AND c.conname = @c;",
                conn);
            ckCmd.Parameters.AddWithValue("c", constraintName);
            var count = (long)(await ckCmd.ExecuteScalarAsync() ?? 0L);
            Assert.True(count == 1L, $"Expected check constraint '{constraintName}' in schema 'governance_intelligence'.");
        }
    }

    private static async Task VerifyComponent1CaseProjectionStorePostgresAsync(string connectionString)
    {
        var createdIds = new List<Guid>();
        try
        {
            await using var dbContext = CreatePostgresDbContext(connectionString);
            var store = new PostgresComponent1GovernanceCaseProjectionStore(dbContext);
            var caseId = $"SLG-PG-C1-{Guid.NewGuid():N}"[..24];
            var provenance = CreatePp1Provenance("Component1.SyntheticFixture", caseId, "PP1-PG-C1-01");

            var sha1 = Component1GovernanceCaseProjection.ComputeCanonicalSha256(
                caseId, "LP-PG-01", "DS-01", "Colombo", "Western", "GN-01", "Commercial", 1.7500m,
                Component1OwnershipEvidenceStatus.EvidenceProvided,
                Component1ProtectedZoneOverlapState.ProtectedZoneOverlapDetected,
                new[] { "ZONE-B", "ZONE-A" },
                Component1BoundaryConfirmationStatus.BoundaryConfirmed,
                true,
                new[] { "REF-2", "REF-1" },
                provenance);

            var projection1 = new Component1GovernanceCaseProjection(
                Guid.NewGuid(), caseId, "LP-PG-01", "DS-01", "Colombo", "Western", "GN-01", "Commercial", 1.7500m,
                Component1OwnershipEvidenceStatus.EvidenceProvided,
                Component1ProtectedZoneOverlapState.ProtectedZoneOverlapDetected,
                new[] { "ZONE-B", "ZONE-A" },
                Component1BoundaryConfirmationStatus.BoundaryConfirmed,
                true,
                new[] { "REF-2", "REF-1" },
                sha1, provenance, FixedUtcNow);

            var persisted1 = await store.UpsertAsync(projection1);
            createdIds.Add(persisted1.ProjectionId);

            // Idempotent duplicate upsert
            var duplicate = await store.UpsertAsync(projection1);
            Assert.Equal(persisted1.ProjectionId, duplicate.ProjectionId);

            // Conflicting payload with same SourceRecordVersion ('v1') must fail closed with InvalidOperationException
            var conflictingShaV1 = Component1GovernanceCaseProjection.ComputeCanonicalSha256(
                caseId, "LP-PG-01", "DS-01", "Colombo", "Western", "GN-01", "Commercial", 2.2500m,
                Component1OwnershipEvidenceStatus.EvidenceProvided,
                Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
                Array.Empty<string>(),
                Component1BoundaryConfirmationStatus.BoundaryConfirmed,
                false,
                new[] { "REF-1" },
                provenance);

            var conflictingProjectionV1 = new Component1GovernanceCaseProjection(
                Guid.NewGuid(), caseId, "LP-PG-01", "DS-01", "Colombo", "Western", "GN-01", "Commercial", 2.2500m,
                Component1OwnershipEvidenceStatus.EvidenceProvided,
                Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
                Array.Empty<string>(),
                Component1BoundaryConfirmationStatus.BoundaryConfirmed,
                false,
                new[] { "REF-1" },
                conflictingShaV1, provenance, FixedUtcNow.AddMinutes(2));

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync(conflictingProjectionV1));

            // Distinct projection for same case with new SourceRecordVersion ('v2') -> not deduplicated
            var provenanceV2 = GovernanceInputProvenance.CreatePp1Fixture(
                sourceSystem: "Component1.SyntheticFixture",
                sourceRecordId: caseId,
                sourceRecordVersion: "v2",
                sourceObservedAtUtc: FixedUtcNow,
                syntheticSeedId: "PP1-PG-C1-02",
                generatedAtUtc: FixedUtcNow);

            var sha2 = Component1GovernanceCaseProjection.ComputeCanonicalSha256(
                caseId, "LP-PG-01", "DS-01", "Colombo", "Western", "GN-01", "Commercial", 2.2500m,
                Component1OwnershipEvidenceStatus.EvidenceProvided,
                Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
                Array.Empty<string>(),
                Component1BoundaryConfirmationStatus.BoundaryConfirmed,
                false,
                new[] { "REF-1" },
                provenanceV2);

            var projection2 = new Component1GovernanceCaseProjection(
                Guid.NewGuid(), caseId, "LP-PG-01", "DS-01", "Colombo", "Western", "GN-01", "Commercial", 2.2500m,
                Component1OwnershipEvidenceStatus.EvidenceProvided,
                Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
                Array.Empty<string>(),
                Component1BoundaryConfirmationStatus.BoundaryConfirmed,
                false,
                new[] { "REF-1" },
                sha2, provenanceV2, FixedUtcNow.AddMinutes(5));

            var persisted2 = await store.UpsertAsync(projection2);
            createdIds.Add(persisted2.ProjectionId);
            Assert.NotEqual(persisted1.ProjectionId, persisted2.ProjectionId);

            // Read from a fresh DbContext to prove JSONB & numeric(14,4) round-trip
            await using var readContext = CreatePostgresDbContext(connectionString);
            var readStore = new PostgresComponent1GovernanceCaseProjectionStore(readContext);
            var fetched1 = await readStore.GetByIdAsync(persisted1.ProjectionId);
            Assert.NotNull(fetched1);
            Assert.Equal(1.7500m, fetched1!.ExtentHa);
            Assert.Equal(new[] { "ZONE-A", "ZONE-B" }, fetched1.OverlappingSensitiveZoneCodes);
            Assert.Equal(new[] { "REF-1", "REF-2" }, fetched1.ParcelEvidenceRefs);

            var history = await readStore.GetByCaseIdAsync(caseId, 10);
            Assert.Equal(2, history.Count);
            Assert.Equal(new[] { persisted2.ProjectionId, persisted1.ProjectionId }, history.Select(x => x.ProjectionId));

            // Verify PostgreSQL check constraint rejects invalid extent_ha <= 0
            await using var badContext = CreatePostgresDbContext(connectionString);
            badContext.Component1CaseProjections.Add(new Component1CaseProjectionEntity
            {
                ProjectionId = Guid.NewGuid(),
                CaseId = caseId,
                LandParcelId = "LP-BAD",
                DivisionCode = "DS-01",
                DistrictName = "Colombo",
                ProvinceName = "Western",
                GnDivisionCode = "GN-01",
                RequestedLandUseCategory = "Commercial",
                ExtentHa = 0m, // Violates ck_c1_case_projections_extent_ha_positive
                OwnershipEvidenceStatus = "EvidenceProvided",
                ProtectedZoneOverlapState = "NoProtectedZoneOverlap",
                OverlappingSensitiveZoneCodesJson = "[]",
                BoundaryConfirmationStatus = "BoundaryConfirmed",
                EncumbranceOrDisputeFlag = false,
                ParcelEvidenceRefsJson = "[\"REF-1\"]",
                InputSha256 = new string('a', 64),
                SourceSystem = "Component1.SyntheticFixture",
                SourceRecordId = "BAD-1",
                SourceRecordVersion = "v1",
                SourceObservedAtUtc = FixedUtcNow,
                IsSynthetic = true,
                ProvenanceKind = "SyntheticFixture",
                SyntheticSeedId = "SEED-1",
                SyntheticSeedVersion = "PP1-SYNTH-2026-10-v1",
                GeneratedAtUtc = FixedUtcNow,
                Purpose = "PP1Demonstration",
                IngestedAtUtc = FixedUtcNow
            });

            var dbEx = await Assert.ThrowsAsync<DbUpdateException>(() => badContext.SaveChangesAsync());
            var pgEx = Assert.IsType<PostgresException>(dbEx.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, pgEx.SqlState);
        }
        finally
        {
            await using var cleanup = CreatePostgresDbContext(connectionString);
            var rows = await cleanup.Component1CaseProjections.Where(x => createdIds.Contains(x.ProjectionId)).ToListAsync();
            cleanup.Component1CaseProjections.RemoveRange(rows);
            await cleanup.SaveChangesAsync();
        }
    }

    private static async Task VerifyComponent3WorkflowProjectionStorePostgresAsync(string connectionString)
    {
        var createdIds = new List<Guid>();
        try
        {
            await using var dbContext = CreatePostgresDbContext(connectionString);
            var store = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
            var workflowRunId = Guid.NewGuid();
            var caseId = $"SLG-PG-C3-{Guid.NewGuid():N}"[..24];
            var provenance = CreatePp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-PG-C3-01");
            var colomboOffset = TimeSpan.FromHours(5.5);

            var events = new[]
            {
                new Component3WorkflowProjectionEvent(
                    "EVT-1", 1, "Application Created", "STAGE_01", "Clerk", "DS_OFFICE",
                    new DateTimeOffset(2031, 2, 3, 8, 0, 0, colomboOffset), "Completed", "REF-1",
                    resource: "SYNTH_OFFICER_01", anomalyInputTimestamp: "2031-02-03T08:00:00"),
                new Component3WorkflowProjectionEvent(
                    "EVT-2", 2, "DS Final Notification", "STAGE_FINAL", "DS", "DS_OFFICE",
                    new DateTimeOffset(2031, 2, 5, 9, 0, 0, colomboOffset), "Approved", "REF-2",
                    resource: "SYNTH_OFFICER_03", anomalyInputTimestamp: "2031-02-05T09:00:00")
            };

            var start = new TimingBoundaryEvidence(events[0].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "C3", "EVT-1", caseId, workflowRunId);
            var handover = new TimingBoundaryEvidence(events[1].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "C3", "EVT-2", caseId, workflowRunId);

            var sha = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
                workflowRunId, caseId, "StateLandLeaseApproval",
                Component3WorkflowTraceStatus.Completed,
                Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
                "STAGE_FINAL",
                Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
                "Asia/Colombo", events, start, handover, provenance);

            var projection = new Component3WorkflowGovernanceProjection(
                Guid.NewGuid(), workflowRunId, caseId, "StateLandLeaseApproval",
                Component3WorkflowTraceStatus.Completed,
                Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
                "STAGE_FINAL",
                Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
                "Asia/Colombo", events, start, handover, sha, provenance, FixedUtcNow);

            var persisted = await store.UpsertAsync(projection);
            createdIds.Add(persisted.ProjectionId);

            // Idempotent duplicate upsert
            var duplicate = await store.UpsertAsync(projection);
            Assert.Equal(persisted.ProjectionId, duplicate.ProjectionId);

            // Read back from fresh PostgreSQL DbContext and verify JSONB round-trip preserves exact event order, Resource, and verbatim naive AnomalyInputTimestamp
            await using var readContext = CreatePostgresDbContext(connectionString);
            var readStore = new PostgresComponent3WorkflowGovernanceProjectionStore(readContext);
            var fetched = await readStore.GetByIdAsync(persisted.ProjectionId);
            Assert.NotNull(fetched);
            Assert.Equal(2, fetched!.Events.Count);
            Assert.Equal("SYNTH_OFFICER_01", fetched.Events[0].Resource);
            Assert.Equal("2031-02-03T08:00:00", fetched.Events[0].AnomalyInputTimestamp);
            Assert.Equal("SYNTH_OFFICER_03", fetched.Events[1].Resource);
            Assert.Equal("2031-02-05T09:00:00", fetched.Events[1].AnomalyInputTimestamp);

            Assert.True(fetched.TryExtractAnomalyScoringEvents(out var extractedEvents, out var err), err);
            Assert.Equal(2, extractedEvents.Count);
            Assert.Equal("2031-02-03T08:00:00", extractedEvents[0].Timestamp);
            Assert.Equal("2031-02-05T09:00:00", extractedEvents[1].Timestamp);
        }
        finally
        {
            await using var cleanup = CreatePostgresDbContext(connectionString);
            var rows = await cleanup.Component3WorkflowProjections.Where(x => createdIds.Contains(x.ProjectionId)).ToListAsync();
            cleanup.Component3WorkflowProjections.RemoveRange(rows);
            await cleanup.SaveChangesAsync();
        }
    }

    private static async Task VerifyWorkflowAnomalyAssessmentStorePostgresAsync(string connectionString)
    {
        var createdIds = new List<Guid>();
        try
        {
            var workflowRunId = Guid.NewGuid();
            var caseId = $"SLG-PG-ANOM-{Guid.NewGuid():N}"[..24];
            var provenance = CreatePp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-PG-ANOM-01");
            var trace = new[]
            {
                new WorkflowAnomalyEvent(1, "Application Created", "DS_OFFICE", "SYNTH_OFFICER_01", "2031-02-03T08:00:00"),
                new WorkflowAnomalyEvent(2, "DS Final Notification", "DS_OFFICE", "SYNTH_OFFICER_03", "2031-02-05T09:00:00")
            };
            var inputSha = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(workflowRunId, caseId, trace, provenance);

            var assessment1 = new WorkflowAnomalyAssessment(
                Guid.NewGuid(), workflowRunId, caseId, null,
                WorkflowAnomalyAssessment.FrozenModelVersionV1,
                WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1,
                anomalyScore: 0.6200,
                threshold: WorkflowAnomalyAssessment.FrozenThresholdV1,
                flagged: true,
                eventCount: 2, elapsedDays: 2.041667, maxGapHours: 49.0, meanGapHours: 49.0,
                uniqueActivities: 2, repeatedActivityCount: 0, resourceHandoffs: 1, institutionSwitches: 0, distinctResources: 2,
                advisoryNote: "Advisory workflow anomaly assessment only.",
                inputSha256: inputSha,
                provenance: provenance,
                assessedAtUtc: FixedUtcNow);

            var assessment1ConcurrentDuplicate = new WorkflowAnomalyAssessment(
                Guid.NewGuid(), workflowRunId, caseId, null,
                WorkflowAnomalyAssessment.FrozenModelVersionV1,
                WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1,
                anomalyScore: 0.6200,
                threshold: WorkflowAnomalyAssessment.FrozenThresholdV1,
                flagged: true,
                eventCount: 2, elapsedDays: 2.041667, maxGapHours: 49.0, meanGapHours: 49.0,
                uniqueActivities: 2, repeatedActivityCount: 0, resourceHandoffs: 1, institutionSwitches: 0, distinctResources: 2,
                advisoryNote: "Advisory workflow anomaly assessment only.",
                inputSha256: inputSha,
                provenance: provenance,
                assessedAtUtc: FixedUtcNow);

            // Concurrent UpsertAsync across two independent DbContexts to verify unique-index race recovery
            await using var ctxA = CreatePostgresDbContext(connectionString);
            await using var ctxB = CreatePostgresDbContext(connectionString);
            var storeA = new PostgresWorkflowAnomalyAssessmentStore(ctxA);
            var storeB = new PostgresWorkflowAnomalyAssessmentStore(ctxB);

            var results = await Task.WhenAll(
                storeA.UpsertAsync(assessment1),
                storeB.UpsertAsync(assessment1ConcurrentDuplicate));

            Assert.Equal(results[0].AssessmentId, results[1].AssessmentId);
            createdIds.Add(results[0].AssessmentId);

            // Verify PostgreSQL check constraint ck_workflow_anomaly_assessments_event_count_positive rejects EventCount <= 0
            await using var badContext = CreatePostgresDbContext(connectionString);
            badContext.WorkflowAnomalyAssessments.Add(new WorkflowAnomalyAssessmentEntity
            {
                AssessmentId = Guid.NewGuid(),
                WorkflowRunId = Guid.NewGuid(),
                CaseId = caseId,
                ModelVersion = WorkflowAnomalyAssessment.FrozenModelVersionV1,
                ModelArtifactSha256 = WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1,
                AnomalyScore = 0.6200,
                Threshold = WorkflowAnomalyAssessment.FrozenThresholdV1,
                Flagged = true,
                EventCount = 0, // Violates ck_workflow_anomaly_assessments_event_count_positive
                ElapsedDays = 1.0,
                MaxGapHours = 24.0,
                MeanGapHours = 24.0,
                UniqueActivities = 2,
                RepeatedActivityCount = 0,
                ResourceHandoffs = 1,
                InstitutionSwitches = 0,
                DistinctResources = 2,
                AdvisoryNote = "Invalid event count test",
                InputSha256 = new string('c', 64),
                SourceSystem = "Component3.SyntheticFixture",
                SourceRecordId = "BAD-ANOM",
                SourceRecordVersion = "v1",
                SourceObservedAtUtc = FixedUtcNow,
                IsSynthetic = true,
                ProvenanceKind = "SyntheticFixture",
                SyntheticSeedId = "SEED-ANOM",
                SyntheticSeedVersion = "PP1-SYNTH-2026-10-v1",
                GeneratedAtUtc = FixedUtcNow,
                Purpose = "PP1Demonstration",
                AssessedAtUtc = FixedUtcNow
            });

            var dbEx = await Assert.ThrowsAsync<DbUpdateException>(() => badContext.SaveChangesAsync());
            var pgEx = Assert.IsType<PostgresException>(dbEx.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, pgEx.SqlState);
        }
        finally
        {
            await using var cleanup = CreatePostgresDbContext(connectionString);
            var rows = await cleanup.WorkflowAnomalyAssessments.Where(x => createdIds.Contains(x.AssessmentId)).ToListAsync();
            cleanup.WorkflowAnomalyAssessments.RemoveRange(rows);
            await cleanup.SaveChangesAsync();
        }
    }

    private static async Task VerifyLeaseApprovalTimingAssessmentStorePostgresAsync(string connectionString)
    {
        var createdIds = new List<Guid>();
        try
        {
            await using var dbContext = CreatePostgresDbContext(connectionString);
            var store = new PostgresLeaseApprovalTimingAssessmentStore(dbContext);
            var workflowRunId = Guid.NewGuid();
            var caseId = $"SLG-PG-TIME-{Guid.NewGuid():N}"[..24];
            var provenance = CreatePp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-PG-TIME-01");
            var colomboOffset = TimeSpan.FromHours(5.5);

            var startEvidence = new TimingBoundaryEvidence(
                new DateTimeOffset(2026, 1, 31, 9, 0, 0, colomboOffset),
                TimingBoundaryEvidenceStatus.Confirmed,
                "Component3",
                "EVT-START",
                caseId,
                workflowRunId);
            var handoverEvidence = new TimingBoundaryEvidence(
                new DateTimeOffset(2026, 4, 2, 16, 0, 0, colomboOffset),
                TimingBoundaryEvidenceStatus.Confirmed,
                "Component3",
                "EVT-HANDOVER",
                caseId,
                workflowRunId);

            var calcInput = new LeaseApprovalTimingAssessmentInput(
                CaseId: caseId,
                WorkflowRunId: workflowRunId,
                WorkflowState: LeaseApprovalWorkflowState.Completed,
                AssessmentAt: FixedUtcNow,
                CompleteProposalRoadmapStart: startEvidence,
                ApprovalHandover: handoverEvidence);

            var inputSha = LeaseApprovalTimingAssessment.ComputeCanonicalInputSha256(calcInput, provenance);

            var timingAssessment = new LeaseApprovalTimingAssessment(
                assessmentId: Guid.NewGuid(),
                workflowRunId: workflowRunId,
                caseId: caseId,
                workflowProjectionId: null,
                policyVersion: "lease_timing_policy_v1",
                timeZoneId: LeaseApprovalTimingAssessment.RequiredColomboTimeZone,
                assessmentDateColombo: new DateOnly(2026, 4, 2),
                completeProposalRoadmapStartDate: new DateOnly(2026, 1, 31),
                expectedApprovalHandoverDate: new DateOnly(2026, 3, 31),
                approvalHandoverDate: new DateOnly(2026, 4, 2),
                daysElapsed: 61,
                daysBeyondExpected: 2,
                timingState: LeaseApprovalTimingState.ExceededExpectedTwoCalendarMonths,
                evidenceState: "Confirmed",
                advisoryNote: "Completed lease-approval handover exceeded the two-calendar-month operational expectation by 2 calendar days.",
                inputSha256: inputSha,
                provenance: provenance,
                assessedAtUtc: FixedUtcNow);

            var persisted = await store.UpsertAsync(timingAssessment);
            createdIds.Add(persisted.AssessmentId);

            var duplicate = await store.UpsertAsync(timingAssessment);
            Assert.Equal(persisted.AssessmentId, duplicate.AssessmentId);

            await using var readContext = CreatePostgresDbContext(connectionString);
            var readStore = new PostgresLeaseApprovalTimingAssessmentStore(readContext);
            var fetched = await readStore.GetByIdAsync(persisted.AssessmentId);
            Assert.NotNull(fetched);
            Assert.Equal(new DateOnly(2026, 1, 31), fetched!.CompleteProposalRoadmapStartDate);
            Assert.Equal(new DateOnly(2026, 3, 31), fetched.ExpectedApprovalHandoverDate);
            Assert.Equal(new DateOnly(2026, 4, 2), fetched.ApprovalHandoverDate);
            Assert.Equal(LeaseApprovalTimingState.ExceededExpectedTwoCalendarMonths, fetched.TimingState);
            Assert.Equal(2, fetched.DaysBeyondExpected);

            // Verify PostgreSQL check constraint ck_lease_approval_timing_assessments_time_zone_colombo rejects non-Colombo time zone
            await using var badContext = CreatePostgresDbContext(connectionString);
            badContext.LeaseApprovalTimingAssessments.Add(new LeaseApprovalTimingAssessmentEntity
            {
                AssessmentId = Guid.NewGuid(),
                WorkflowRunId = Guid.NewGuid(),
                CaseId = caseId,
                PolicyVersion = "lease_timing_policy_v1",
                TimeZoneId = "UTC", // Violates ck_lease_approval_timing_assessments_time_zone_colombo
                AssessmentDateColombo = new DateOnly(2026, 4, 2),
                TimingState = "UnavailableEvidence",
                EvidenceState = "Unavailable",
                AdvisoryNote = "Invalid timezone test",
                InputSha256 = new string('d', 64),
                SourceSystem = "Component3.SyntheticFixture",
                SourceRecordId = "BAD-TIME",
                SourceRecordVersion = "v1",
                SourceObservedAtUtc = FixedUtcNow,
                IsSynthetic = true,
                ProvenanceKind = "SyntheticFixture",
                SyntheticSeedId = "SEED-TIME",
                SyntheticSeedVersion = "PP1-SYNTH-2026-10-v1",
                GeneratedAtUtc = FixedUtcNow,
                Purpose = "PP1Demonstration",
                AssessedAtUtc = FixedUtcNow
            });

            var dbEx = await Assert.ThrowsAsync<DbUpdateException>(() => badContext.SaveChangesAsync());
            var pgEx = Assert.IsType<PostgresException>(dbEx.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, pgEx.SqlState);
        }
        finally
        {
            await using var cleanup = CreatePostgresDbContext(connectionString);
            var rows = await cleanup.LeaseApprovalTimingAssessments.Where(x => createdIds.Contains(x.AssessmentId)).ToListAsync();
            cleanup.LeaseApprovalTimingAssessments.RemoveRange(rows);
            await cleanup.SaveChangesAsync();
        }
    }

    private static string GetValidatedTestDatabaseConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Real PostgreSQL test database connection is required. Set {ConnectionEnvVar} to an isolated database starting with '{AllowedTestDatabasePrefix}'.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !builder.Database.StartsWith(AllowedTestDatabasePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run integration tests against non-isolated database '{builder.Database}'. Database name must start with '{AllowedTestDatabasePrefix}'.");
        }

        return connectionString;
    }

    private static GovernanceIntelligenceDbContext CreatePostgresDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(GovernanceIntelligenceDbContext).Assembly.GetName().Name);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", GovernanceIntelligenceDbContext.SchemaName);
                })
            .Options;

        return new GovernanceIntelligenceDbContext(options);
    }

    private static GovernanceInputProvenance CreatePp1Provenance(
        string sourceSystem,
        string sourceRecordId,
        string syntheticSeedId) =>
        GovernanceInputProvenance.CreatePp1Fixture(
            sourceSystem: sourceSystem,
            sourceRecordId: sourceRecordId,
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            syntheticSeedId: syntheticSeedId,
            generatedAtUtc: FixedUtcNow);

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StateLandGovernance.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "StateLandGovernance.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
