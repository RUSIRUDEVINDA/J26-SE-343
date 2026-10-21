using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed store for Component 1 governance case projections.
/// Enforces idempotent ingestion by <c>(CaseId, SourceRecordVersion, InputSha256)</c>.
/// </summary>
public sealed class PostgresComponent1GovernanceCaseProjectionStore
    : IComponent1GovernanceCaseProjectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresComponent1GovernanceCaseProjectionStore(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Component1GovernanceCaseProjection> UpsertAsync(
        Component1GovernanceCaseProjection projection,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(projection);

        var existing = await FindExistingOrThrowConflictAsync(projection, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var entity = MapToEntity(projection);
        await _dbContext.Component1CaseProjections.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return projection;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _dbContext.Entry(entity).State = EntityState.Detached;
            var concurrentWinner = await FindExistingOrThrowConflictAsync(projection, cancellationToken).ConfigureAwait(false);
            if (concurrentWinner is not null)
            {
                return concurrentWinner;
            }

            throw;
        }
    }

    public async Task<Component1GovernanceCaseProjection?> GetByIdAsync(
        Guid projectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (projectionId == Guid.Empty)
        {
            throw new ArgumentException("ProjectionId cannot be empty.", nameof(projectionId));
        }

        var entity = await _dbContext.Component1CaseProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProjectionId == projectionId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Component1GovernanceCaseProjection?> GetByCaseIdAndVersionAsync(
        string caseId,
        string sourceRecordVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("CaseId cannot be null or whitespace.", nameof(caseId));
        }

        if (string.IsNullOrWhiteSpace(sourceRecordVersion))
        {
            throw new ArgumentException("SourceRecordVersion cannot be null or whitespace.", nameof(sourceRecordVersion));
        }

        var normalizedCaseId = caseId.Trim();
        var normalizedVersion = sourceRecordVersion.Trim();

        var entity = await _dbContext.Component1CaseProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.CaseId == normalizedCaseId && x.SourceRecordVersion == normalizedVersion,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<Component1GovernanceCaseProjection>> GetByCaseIdAsync(
        string caseId,
        int limit = IComponent1GovernanceCaseProjectionStore.DefaultHistoryLimit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("CaseId cannot be null or whitespace.", nameof(caseId));
        }

        if (limit is < 1 or > IComponent1GovernanceCaseProjectionStore.MaxHistoryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {IComponent1GovernanceCaseProjectionStore.MaxHistoryLimit}.");
        }

        var normalizedCaseId = caseId.Trim();
        var entities = await _dbContext.Component1CaseProjections
            .AsNoTracking()
            .Where(x => x.CaseId == normalizedCaseId)
            .OrderByDescending(x => x.IngestedAtUtc)
            .ThenByDescending(x => x.ProjectionId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    private async Task<Component1GovernanceCaseProjection?> FindExistingOrThrowConflictAsync(
        Component1GovernanceCaseProjection projection,
        CancellationToken cancellationToken)
    {
        var existingForVersion = await _dbContext.Component1CaseProjections
            .AsNoTracking()
            .Where(x => x.CaseId == projection.CaseId &&
                        x.SourceRecordVersion == projection.Provenance.SourceRecordVersion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var exactMatch = existingForVersion.FirstOrDefault(
            x => string.Equals(x.InputSha256, projection.InputSha256, StringComparison.Ordinal));
        if (exactMatch is not null)
        {
            return MapToDomain(exactMatch);
        }

        if (existingForVersion.Count > 0)
        {
            throw new InvalidOperationException(
                $"Conflicting Component 1 projection already exists for CaseId '{projection.CaseId}' " +
                $"and SourceRecordVersion '{projection.Provenance.SourceRecordVersion}' with a different InputSha256.");
        }

        return null;
    }

    private static Component1CaseProjectionEntity MapToEntity(Component1GovernanceCaseProjection projection)
    {
        return new Component1CaseProjectionEntity
        {
            ProjectionId = projection.ProjectionId,
            CaseId = projection.CaseId,
            LandParcelId = projection.LandParcelId,
            DivisionCode = projection.DivisionCode,
            DistrictName = projection.DistrictName,
            ProvinceName = projection.ProvinceName,
            GnDivisionCode = projection.GnDivisionCode,
            RequestedLandUseCategory = projection.RequestedLandUseCategory,
            ExtentHa = projection.ExtentHa,
            OwnershipEvidenceStatus = projection.OwnershipEvidenceStatus.ToString(),
            ProtectedZoneOverlapState = projection.ProtectedZoneOverlapState.ToString(),
            OverlappingSensitiveZoneCodesJson = JsonSerializer.Serialize(projection.OverlappingSensitiveZoneCodes, JsonOptions),
            BoundaryConfirmationStatus = projection.BoundaryConfirmationStatus.ToString(),
            EncumbranceOrDisputeFlag = projection.EncumbranceOrDisputeFlag,
            ParcelEvidenceRefsJson = JsonSerializer.Serialize(projection.ParcelEvidenceRefs, JsonOptions),
            InputSha256 = projection.InputSha256,
            SourceSystem = projection.Provenance.SourceSystem,
            SourceRecordId = projection.Provenance.SourceRecordId,
            SourceRecordVersion = projection.Provenance.SourceRecordVersion,
            SourceObservedAtUtc = projection.Provenance.SourceObservedAtUtc,
            IsSynthetic = projection.Provenance.IsSynthetic,
            ProvenanceKind = projection.Provenance.ProvenanceKind.ToString(),
            SyntheticSeedId = projection.Provenance.SyntheticSeedId,
            SyntheticSeedVersion = projection.Provenance.SyntheticSeedVersion,
            GeneratedAtUtc = projection.Provenance.GeneratedAtUtc,
            Purpose = projection.Provenance.Purpose.ToString(),
            IngestedAtUtc = projection.IngestedAtUtc
        };
    }

    private static Component1GovernanceCaseProjection MapToDomain(Component1CaseProjectionEntity entity)
    {
        var zoneCodes = JsonSerializer.Deserialize<List<string>>(entity.OverlappingSensitiveZoneCodesJson, JsonOptions)
            ?? throw new InvalidOperationException($"Component 1 projection '{entity.ProjectionId}' has null OverlappingSensitiveZoneCodesJson.");
        var evidenceRefs = JsonSerializer.Deserialize<List<string>>(entity.ParcelEvidenceRefsJson, JsonOptions)
            ?? throw new InvalidOperationException($"Component 1 projection '{entity.ProjectionId}' has null ParcelEvidenceRefsJson.");

        var provenance = new GovernanceInputProvenance(
            sourceSystem: entity.SourceSystem,
            sourceRecordId: entity.SourceRecordId,
            sourceRecordVersion: entity.SourceRecordVersion,
            sourceObservedAtUtc: entity.SourceObservedAtUtc,
            isSynthetic: entity.IsSynthetic,
            provenanceKind: Enum.Parse<GovernanceProvenanceKind>(entity.ProvenanceKind, ignoreCase: false),
            syntheticSeedId: entity.SyntheticSeedId,
            syntheticSeedVersion: entity.SyntheticSeedVersion,
            generatedAtUtc: entity.GeneratedAtUtc,
            purpose: Enum.Parse<GovernanceProvenancePurpose>(entity.Purpose, ignoreCase: false));

        return new Component1GovernanceCaseProjection(
            projectionId: entity.ProjectionId,
            caseId: entity.CaseId,
            landParcelId: entity.LandParcelId,
            divisionCode: entity.DivisionCode,
            districtName: entity.DistrictName,
            provinceName: entity.ProvinceName,
            gnDivisionCode: entity.GnDivisionCode,
            requestedLandUseCategory: entity.RequestedLandUseCategory,
            extentHa: entity.ExtentHa,
            ownershipEvidenceStatus: Enum.Parse<Component1OwnershipEvidenceStatus>(entity.OwnershipEvidenceStatus, ignoreCase: false),
            protectedZoneOverlapState: Enum.Parse<Component1ProtectedZoneOverlapState>(entity.ProtectedZoneOverlapState, ignoreCase: false),
            overlappingSensitiveZoneCodes: zoneCodes,
            boundaryConfirmationStatus: Enum.Parse<Component1BoundaryConfirmationStatus>(entity.BoundaryConfirmationStatus, ignoreCase: false),
            encumbranceOrDisputeFlag: entity.EncumbranceOrDisputeFlag,
            parcelEvidenceRefs: evidenceRefs,
            inputSha256: entity.InputSha256,
            provenance: provenance,
            ingestedAtUtc: entity.IngestedAtUtc);
    }
}
