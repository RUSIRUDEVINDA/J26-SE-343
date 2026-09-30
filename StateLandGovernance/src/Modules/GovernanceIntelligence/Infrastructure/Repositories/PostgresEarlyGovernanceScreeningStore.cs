using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL implementation of the <see cref="IEarlyGovernanceScreeningStore"/> using Entity Framework Core.
/// <para>
/// Operational Guarantee:
/// This store offers insert and read operations for historical evaluation snapshots. The primary key prevents a
/// duplicate assessment ID from being inserted, but it does not prevent SQL updates or other out-of-band modifications,
/// and it does not provide cryptographic integrity verification.
/// </para>
/// </summary>
public sealed class PostgresEarlyGovernanceScreeningStore : IEarlyGovernanceScreeningStore
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresEarlyGovernanceScreeningStore(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(
        Guid assessmentId,
        DateTimeOffset createdAtUtc,
        EarlyGovernanceScreeningResultDto result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (assessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("CreatedAtUtc must have a zero UTC offset.", nameof(createdAtUtc));
        }

        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (string.IsNullOrWhiteSpace(result.CaseId))
        {
            throw new ArgumentException("Result CaseId cannot be blank.", nameof(result));
        }

        if (string.IsNullOrWhiteSpace(result.InputVersion))
        {
            throw new ArgumentException("Result InputVersion cannot be blank.", nameof(result));
        }

        if (result.IndicatorResults is null)
        {
            throw new ArgumentException("Result IndicatorResults cannot be null.", nameof(result));
        }

        if (result.IndicatorResults.Any(r => r is null))
        {
            throw new ArgumentException("Result IndicatorResults cannot contain null entries.", nameof(result));
        }

        // Serialize immediately to guarantee caller isolation against subsequent mutations
        var snapshotJson = JsonSerializer.Serialize(result, JsonOptions);

        var entity = new EarlyGovernanceScreeningEvaluationEntity
        {
            AssessmentId = assessmentId,
            CaseId = result.CaseId,
            InputVersion = result.InputVersion,
            CreatedAtUtc = createdAtUtc,
            SnapshotSchemaVersion = CurrentSchemaVersion,
            ResultSnapshotJson = snapshotJson
        };

        await _dbContext.EarlyGovernanceScreeningEvaluations.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredEarlyGovernanceScreeningDto?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (assessmentId == Guid.Empty)
        {
            throw new ArgumentException("AssessmentId cannot be empty.", nameof(assessmentId));
        }

        var entity = await _dbContext.EarlyGovernanceScreeningEvaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AssessmentId == assessmentId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        if (entity.SnapshotSchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported snapshot schema version '{entity.SnapshotSchemaVersion}' for assessment '{assessmentId}'. Supported version is '{CurrentSchemaVersion}'.");
        }

        EarlyGovernanceScreeningResultDto? resultDto;
        try
        {
            ValidateSnapshotStructure(assessmentId, entity.ResultSnapshotJson);
            resultDto = JsonSerializer.Deserialize<EarlyGovernanceScreeningResultDto>(entity.ResultSnapshotJson, JsonOptions);
        }
        catch (JsonException)
        {
            // Omit raw inner JsonException to guarantee sensitive property names or tokens are never leaked via ToString()
            throw new InvalidOperationException(
                $"Snapshot for assessment '{assessmentId}' is malformed and could not be deserialized.");
        }

        if (resultDto is null)
        {
            throw new InvalidOperationException(
                $"Snapshot for assessment '{assessmentId}' deserialized to a null result.");
        }

        if (!string.Equals(resultDto.CaseId, entity.CaseId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Snapshot integrity violation: result CaseId does not match stored entity CaseId for assessment '{assessmentId}'.");
        }

        if (!string.Equals(resultDto.InputVersion, entity.InputVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Snapshot integrity violation: result InputVersion does not match stored entity InputVersion for assessment '{assessmentId}'.");
        }

        return new StoredEarlyGovernanceScreeningDto(
            entity.AssessmentId,
            entity.CreatedAtUtc,
            entity.SnapshotSchemaVersion,
            resultDto);
    }

    private static void ValidateSnapshotStructure(Guid assessmentId, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Snapshot for assessment '{assessmentId}' is invalid: root must be a JSON object.");
        }

        RequireNonBlankStringProperty(root, "caseId", assessmentId);
        RequireNonBlankStringProperty(root, "inputVersion", assessmentId);
        RequireNonBlankStringProperty(root, "overallStatus", assessmentId);
        RequireBooleanProperty(root, "hasIncompleteEvidence", assessmentId);

        if (!root.TryGetProperty("indicatorResults", out var indicatorResultsElement) ||
            indicatorResultsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"Snapshot for assessment '{assessmentId}' is invalid: missing or invalid 'indicatorResults' array.");
        }

        int index = 0;
        foreach (var indicatorElement in indicatorResultsElement.EnumerateArray())
        {
            if (indicatorElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Snapshot for assessment '{assessmentId}' is invalid: indicator at index {index} must be a JSON object.");
            }

            RequireNonBlankStringProperty(indicatorElement, "indicatorType", assessmentId, index);
            RequireNonBlankStringProperty(indicatorElement, "evidenceState", assessmentId, index);
            RequireBooleanProperty(indicatorElement, "requiresReview", assessmentId, index);
            RequireBooleanProperty(indicatorElement, "needsEvidence", assessmentId, index);
            RequireNonBlankStringProperty(indicatorElement, "reasonCode", assessmentId, index);
            RequireNonBlankStringProperty(indicatorElement, "message", assessmentId, index);

            index++;
        }
    }

    private static void RequireNonBlankStringProperty(
        JsonElement element,
        string propertyName,
        Guid assessmentId,
        int? indicatorIndex = null)
    {
        if (!element.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(prop.GetString()))
        {
            var location = indicatorIndex.HasValue ? $"indicator at index {indicatorIndex.Value}" : "root";
            throw new InvalidOperationException(
                $"Snapshot for assessment '{assessmentId}' is invalid: missing or blank '{propertyName}' in {location}.");
        }
    }

    private static void RequireBooleanProperty(
        JsonElement element,
        string propertyName,
        Guid assessmentId,
        int? indicatorIndex = null)
    {
        if (!element.TryGetProperty(propertyName, out var prop) ||
            (prop.ValueKind != JsonValueKind.True && prop.ValueKind != JsonValueKind.False))
        {
            var location = indicatorIndex.HasValue ? $"indicator at index {indicatorIndex.Value}" : "root";
            throw new InvalidOperationException(
                $"Snapshot for assessment '{assessmentId}' is invalid: missing or invalid boolean '{propertyName}' in {location}.");
        }
    }
}
