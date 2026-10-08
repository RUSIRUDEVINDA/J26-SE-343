using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class EarlyGovernanceScreeningStoreTests
{
    private static readonly DateTimeOffset SampleUtcTime = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions CamelCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static DbContextOptions<GovernanceIntelligenceDbContext> CreateInMemoryOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private static EarlyGovernanceScreeningResultDto CreateSampleResult(
        string caseId = "CASE-2026-001",
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
    public async Task A_CompleteRoundTrip_PersistsAndRetrievesAllFieldsAndOrdering()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();
        var originalResult = CreateSampleResult();

        // Act 1: Save using initial context
        using (var writeContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(writeContext);
            await store.AddAsync(assessmentId, SampleUtcTime, originalResult);
        }

        // Act 2: Retrieve using fresh context
        StoredEarlyGovernanceScreeningDto? stored;
        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            stored = await store.GetByIdAsync(assessmentId);
        }

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(assessmentId, stored.AssessmentId);
        Assert.Equal(SampleUtcTime, stored.CreatedAtUtc);
        Assert.Equal(1, stored.SnapshotSchemaVersion);

        var retrieved = stored.Result;
        Assert.NotNull(retrieved);
        Assert.Equal(originalResult.CaseId, retrieved.CaseId);
        Assert.Equal(originalResult.InputVersion, retrieved.InputVersion);
        Assert.Equal(originalResult.OverallStatus, retrieved.OverallStatus);
        Assert.Equal(originalResult.HasIncompleteEvidence, retrieved.HasIncompleteEvidence);
        Assert.Equal(originalResult.IndicatorResults.Count, retrieved.IndicatorResults.Count);

        for (int i = 0; i < originalResult.IndicatorResults.Count; i++)
        {
            var expected = originalResult.IndicatorResults[i];
            var actual = retrieved.IndicatorResults[i];

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

    [Fact]
    public async Task B_HistoricalSeparation_PreservesMultipleAssessmentsForSameCaseAndVersion()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);

        var assessment1 = Guid.NewGuid();
        var assessment2 = Guid.NewGuid();
        var time1 = SampleUtcTime;
        var time2 = SampleUtcTime.AddHours(2);

        var result1 = CreateSampleResult("CASE-HIST-01", "v1");
        var result2 = CreateSampleResult("CASE-HIST-01", "v1");

        using (var writeContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(writeContext);
            await store.AddAsync(assessment1, time1, result1);
            await store.AddAsync(assessment2, time2, result2);
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);

            var read1 = await store.GetByIdAsync(assessment1);
            var read2 = await store.GetByIdAsync(assessment2);

            Assert.NotNull(read1);
            Assert.NotNull(read2);
            Assert.Equal(assessment1, read1.AssessmentId);
            Assert.Equal(assessment2, read2.AssessmentId);
            Assert.Equal(time1, read1.CreatedAtUtc);
            Assert.Equal(time2, read2.CreatedAtUtc);
            Assert.Equal("CASE-HIST-01", read1.Result.CaseId);
            Assert.Equal("CASE-HIST-01", read2.Result.CaseId);
        }
    }

    [Fact]
    public async Task C_UnknownId_ReturnsNull()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);

        using var context = new GovernanceIntelligenceDbContext(options);
        var store = new PostgresEarlyGovernanceScreeningStore(context);

        var result = await store.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task D_DuplicateAssessmentId_FailsAndLeavesOriginalIntact()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var originalResult = CreateSampleResult("CASE-ORIGINAL", "v1");
        var duplicateResult = CreateSampleResult("CASE-DUPLICATE", "v2");

        using (var context1 = new GovernanceIntelligenceDbContext(options))
        {
            var store1 = new PostgresEarlyGovernanceScreeningStore(context1);
            await store1.AddAsync(assessmentId, SampleUtcTime, originalResult);
        }

        // Second attempt with same ID using a fresh context
        using (var context2 = new GovernanceIntelligenceDbContext(options))
        {
            var store2 = new PostgresEarlyGovernanceScreeningStore(context2);
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await store2.AddAsync(assessmentId, SampleUtcTime.AddDays(1), duplicateResult);
            });
        }

        // Fresh read confirms original snapshot is intact
        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var retrieved = await store.GetByIdAsync(assessmentId);

            Assert.NotNull(retrieved);
            Assert.Equal("CASE-ORIGINAL", retrieved.Result.CaseId);
            Assert.Equal(SampleUtcTime, retrieved.CreatedAtUtc);
        }
    }

    [Fact]
    public async Task E_InvalidStorageMetadata_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        using var context = new GovernanceIntelligenceDbContext(options);
        var store = new PostgresEarlyGovernanceScreeningStore(context);
        var validResult = CreateSampleResult();

        // Empty AssessmentId
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.AddAsync(Guid.Empty, SampleUtcTime, validResult));

        // Non-zero UTC offset
        var nonUtc = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(5));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.AddAsync(Guid.NewGuid(), nonUtc, validResult));

        // Null result
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.AddAsync(Guid.NewGuid(), SampleUtcTime, null!));

        // Blank CaseId
        var blankCaseResult = validResult with { CaseId = "   " };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.AddAsync(Guid.NewGuid(), SampleUtcTime, blankCaseResult));

        // Blank InputVersion
        var blankVersionResult = validResult with { InputVersion = "" };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.AddAsync(Guid.NewGuid(), SampleUtcTime, blankVersionResult));

        // Null IndicatorResults collection
        var nullCollectionResult = validResult with { IndicatorResults = null! };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.AddAsync(Guid.NewGuid(), SampleUtcTime, nullCollectionResult));

        // Null entry inside IndicatorResults
        var listWithNull = validResult.IndicatorResults.ToList();
        listWithNull[0] = null!;
        var nullEntryResult = validResult with { IndicatorResults = listWithNull };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.AddAsync(Guid.NewGuid(), SampleUtcTime, nullEntryResult));

        // GetById with Guid.Empty
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.GetByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task F_Cancellation_PreCancelledTokenDoesNotWriteOrReturn()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();
        var result = CreateSampleResult();

        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();

            using var context = new GovernanceIntelligenceDbContext(options);
            var store = new PostgresEarlyGovernanceScreeningStore(context);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                store.AddAsync(assessmentId, SampleUtcTime, result, cts.Token));

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                store.GetByIdAsync(assessmentId, cts.Token));
        }

        // Verify nothing was saved
        using (var verifyContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(verifyContext);
            var retrieved = await store.GetByIdAsync(assessmentId);
            Assert.Null(retrieved);
        }
    }

    [Fact]
    public async Task G_UnsupportedSchemaVersion_ThrowsExplicitException()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            var entity = new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-VER",
                InputVersion = "v1",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 99,
                ResultSnapshotJson = "{}"
            };

            await context.EarlyGovernanceScreeningEvaluations.AddAsync(entity);
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("Unsupported snapshot schema version", ex.Message);
        }
    }

    [Fact]
    public async Task H_MalformedStoredJson_ThrowsExplicitExceptionWithoutExposingJsonPayload()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();
        const string secretPayload = "CONFIDENTIAL_PAYLOAD_MARKER_987654";

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            var entity = new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-MALFORMED",
                InputVersion = "v1",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = $"{{ malformed_json_with_{secretPayload}"
            };

            await context.EarlyGovernanceScreeningEvaluations.AddAsync(entity);
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("malformed", ex.Message);
            Assert.DoesNotContain(secretPayload, ex.Message);
            Assert.DoesNotContain(secretPayload, ex.ToString());
        }
    }

    [Fact]
    public async Task Regression_A_ColumnAgreement_MatchingCamelCaseJsonAndColumns_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var validResult = CreateSampleResult("CASE-CORRECT", "v1.0");
        var camelCaseJson = JsonSerializer.Serialize(validResult, CamelCaseJsonOptions);

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-CORRECT",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = camelCaseJson
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var stored = await store.GetByIdAsync(assessmentId);

            Assert.NotNull(stored);
            Assert.Equal("CASE-CORRECT", stored.Result.CaseId);
            Assert.Equal("v1.0", stored.Result.InputVersion);
        }
    }

    [Fact]
    public async Task Regression_B_ColumnDisagreement_CaseIdOnlyMismatch_ThrowsCaseMismatchError()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var validResult = CreateSampleResult("CASE-CORRECT", "v1.0");
        var camelCaseJson = JsonSerializer.Serialize(validResult, CamelCaseJsonOptions);

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-MISMATCHED-COLUMN",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = camelCaseJson
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("result CaseId does not match stored entity CaseId", ex.Message);
            Assert.DoesNotContain("InputVersion", ex.Message);
        }
    }

    [Fact]
    public async Task Regression_C_ColumnDisagreement_InputVersionOnlyMismatch_ThrowsVersionMismatchError()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var validResult = CreateSampleResult("CASE-CORRECT", "v1.0");
        var camelCaseJson = JsonSerializer.Serialize(validResult, CamelCaseJsonOptions);

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-CORRECT",
                InputVersion = "v2.0-MISMATCHED",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = camelCaseJson
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("result InputVersion does not match stored entity InputVersion", ex.Message);
            Assert.DoesNotContain("CaseId", ex.Message);
        }
    }

    [Fact]
    public async Task Regression_D_IncompleteSnapshot_MissingOverallStatus_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var json = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "hasIncompleteEvidence": false,
            "indicatorResults": []
        }
        """;

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = json
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("overallStatus", ex.Message);
        }
    }

    [Fact]
    public async Task Regression_E_IncompleteSnapshot_MissingOrNullIndicatorResults_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId1 = Guid.NewGuid();
        var assessmentId2 = Guid.NewGuid();

        var missingArrayJson = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "hasIncompleteEvidence": false
        }
        """;

        var nullArrayJson = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "hasIncompleteEvidence": false,
            "indicatorResults": null
        }
        """;

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId1,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = missingArrayJson
            });

            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId2,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = nullArrayJson
            });

            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);

            var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId1));
            Assert.Contains("indicatorResults", ex1.Message);

            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId2));
            Assert.Contains("indicatorResults", ex2.Message);
        }
    }

    [Fact]
    public async Task Regression_F_IncompleteSnapshot_MissingHasIncompleteEvidence_IsRejectedRatherThanDefaulted()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var json = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "indicatorResults": []
        }
        """;

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = json
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("hasIncompleteEvidence", ex.Message);
        }
    }

    [Fact]
    public async Task Regression_G_IncompleteSnapshot_NullIndicatorEntry_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var json = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "hasIncompleteEvidence": false,
            "indicatorResults": [
                null
            ]
        }
        """;

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = json
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("indicator at index 0 must be a JSON object", ex.Message);
        }
    }

    [Fact]
    public async Task Regression_H_IncompleteSnapshot_MissingRequiresReviewOrNeedsEvidence_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId1 = Guid.NewGuid();
        var assessmentId2 = Guid.NewGuid();

        var missingRequiresReviewJson = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "hasIncompleteEvidence": false,
            "indicatorResults": [
                {
                    "indicatorType": "LegalDispute",
                    "evidenceState": "VerifiedAbsent",
                    "needsEvidence": false,
                    "reasonCode": "CODE_1",
                    "message": "Message 1"
                }
            ]
        }
        """;

        var missingNeedsEvidenceJson = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "hasIncompleteEvidence": false,
            "indicatorResults": [
                {
                    "indicatorType": "LegalDispute",
                    "evidenceState": "VerifiedAbsent",
                    "requiresReview": false,
                    "reasonCode": "CODE_1",
                    "message": "Message 1"
                }
            ]
        }
        """;

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId1,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = missingRequiresReviewJson
            });

            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId2,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = missingNeedsEvidenceJson
            });

            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);

            var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId1));
            Assert.Contains("requiresReview", ex1.Message);

            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId2));
            Assert.Contains("needsEvidence", ex2.Message);
        }
    }

    [Fact]
    public async Task Regression_I_IncompleteSnapshot_MissingRequiredDescriptiveField_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var missingMessageJson = """
        {
            "caseId": "CASE-100",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "hasIncompleteEvidence": false,
            "indicatorResults": [
                {
                    "indicatorType": "LegalDispute",
                    "evidenceState": "VerifiedAbsent",
                    "requiresReview": false,
                    "needsEvidence": false,
                    "reasonCode": "CODE_1"
                }
            ]
        }
        """;

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-100",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = missingMessageJson
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("message", ex.Message);
        }
    }

    [Fact]
    public async Task Regression_J_ExplicitFalseValues_AreAcceptedAsValid()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var json = """
        {
            "caseId": "CASE-FALSE-TEST",
            "inputVersion": "v1.0",
            "overallStatus": "Clear",
            "hasIncompleteEvidence": false,
            "indicatorResults": [
                {
                    "indicatorType": "LegalDispute",
                    "evidenceState": "VerifiedAbsent",
                    "requiresReview": false,
                    "needsEvidence": false,
                    "reasonCode": "CODE_FALSE",
                    "message": "Message False",
                    "evidenceReference": "DOC-REF",
                    "recordedAtUtc": "2026-09-18T10:00:00+00:00"
                }
            ]
        }
        """;

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-FALSE-TEST",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = json
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var stored = await store.GetByIdAsync(assessmentId);

            Assert.NotNull(stored);
            Assert.False(stored.Result.HasIncompleteEvidence);
            Assert.Single(stored.Result.IndicatorResults);
            Assert.False(stored.Result.IndicatorResults[0].RequiresReview);
            Assert.False(stored.Result.IndicatorResults[0].NeedsEvidence);
        }
    }

    [Fact]
    public async Task Regression_K_MalformedNestedJsonWithConfidentialMarker_DoesNotLeakMarkerInMessageOrToString()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();
        const string secretMarker = "CONFIDENTIAL_TOKEN_TOP_SECRET_987654";

        using (var context = new GovernanceIntelligenceDbContext(options))
        {
            await context.EarlyGovernanceScreeningEvaluations.AddAsync(new EarlyGovernanceScreeningEvaluationEntity
            {
                AssessmentId = assessmentId,
                CaseId = "CASE-SECRET",
                InputVersion = "v1.0",
                CreatedAtUtc = SampleUtcTime,
                SnapshotSchemaVersion = 1,
                ResultSnapshotJson = $"{{\"caseId\": \"CASE-SECRET\", \"unrecognizedProperty_{secretMarker}\": {{ unclosed_nested_json..."
            });
            await context.SaveChangesAsync();
        }

        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.GetByIdAsync(assessmentId));

            Assert.Contains("malformed", ex.Message);
            Assert.DoesNotContain(secretMarker, ex.Message);
            Assert.DoesNotContain(secretMarker, ex.ToString());
        }
    }

    [Fact]
    public async Task J_SerializationIsolation_PostAddMutationDoesNotAffectStoredSnapshot()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = CreateInMemoryOptions(dbName);
        var assessmentId = Guid.NewGuid();

        var mutableList = new List<EarlyGovernanceIndicatorResultDto>
        {
            new("LegalDispute", "VerifiedPresent", true, false, "CODE_1", "Message 1")
        };

        var callerResult = new EarlyGovernanceScreeningResultDto(
            CaseId: "CASE-MUTABLE",
            InputVersion: "v1.0",
            OverallStatus: "ReviewRequired",
            HasIncompleteEvidence: false,
            IndicatorResults: mutableList
        );

        using (var writeContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(writeContext);
            await store.AddAsync(assessmentId, SampleUtcTime, callerResult);
        }

        // Caller mutates their list after AddAsync
        mutableList.Add(new("UnauthorizedOccupation", "Missing", false, true, "CODE_2", "Message 2"));

        // Retrieve and confirm stored snapshot still has 1 indicator
        using (var readContext = new GovernanceIntelligenceDbContext(options))
        {
            var store = new PostgresEarlyGovernanceScreeningStore(readContext);
            var retrieved = await store.GetByIdAsync(assessmentId);

            Assert.NotNull(retrieved);
            Assert.Single(retrieved.Result.IndicatorResults);
            Assert.Equal("LegalDispute", retrieved.Result.IndicatorResults[0].IndicatorType);
        }
    }

    [Fact]
    public void K_ConfigurationMetadata_ConfiguresCorrectTableSchemaAndColumnTypes()
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new GovernanceIntelligenceDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(EarlyGovernanceScreeningEvaluationEntity));

        Assert.NotNull(entityType);
        Assert.Equal("early_governance_screening_evaluations", entityType.GetTableName());
        Assert.Equal("governance_intelligence", entityType.GetSchema());

        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal("assessment_id", pk.Properties[0].GetColumnName());

        var caseIdProp = entityType.FindProperty(nameof(EarlyGovernanceScreeningEvaluationEntity.CaseId));
        Assert.NotNull(caseIdProp);
        Assert.Equal("case_id", caseIdProp.GetColumnName());
        Assert.Equal(100, caseIdProp.GetMaxLength());
        Assert.False(caseIdProp.IsNullable);

        var inputVersionProp = entityType.FindProperty(nameof(EarlyGovernanceScreeningEvaluationEntity.InputVersion));
        Assert.NotNull(inputVersionProp);
        Assert.Equal("input_version", inputVersionProp.GetColumnName());
        Assert.Equal(50, inputVersionProp.GetMaxLength());
        Assert.False(inputVersionProp.IsNullable);

        var createdAtProp = entityType.FindProperty(nameof(EarlyGovernanceScreeningEvaluationEntity.CreatedAtUtc));
        Assert.NotNull(createdAtProp);
        Assert.Equal("created_at_utc", createdAtProp.GetColumnName());
        Assert.Equal("timestamp with time zone", createdAtProp.FindAnnotation("Relational:ColumnType")?.Value);
        Assert.False(createdAtProp.IsNullable);

        var schemaVersionProp = entityType.FindProperty(nameof(EarlyGovernanceScreeningEvaluationEntity.SnapshotSchemaVersion));
        Assert.NotNull(schemaVersionProp);
        Assert.Equal("snapshot_schema_version", schemaVersionProp.GetColumnName());
        Assert.False(schemaVersionProp.IsNullable);

        var jsonProp = entityType.FindProperty(nameof(EarlyGovernanceScreeningEvaluationEntity.ResultSnapshotJson));
        Assert.NotNull(jsonProp);
        Assert.Equal("result_snapshot_json", jsonProp.GetColumnName());
        Assert.Equal("jsonb", jsonProp.FindAnnotation("Relational:ColumnType")?.Value);
        Assert.False(jsonProp.IsNullable);
    }
}
