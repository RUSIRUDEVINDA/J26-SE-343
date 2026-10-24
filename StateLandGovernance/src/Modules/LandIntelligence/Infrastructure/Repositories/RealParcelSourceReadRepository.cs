using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

public sealed class RealParcelSourceReadRepository : IRealParcelSourceReadRepository
{
    private readonly LandIntelligenceDbContext _dbContext;

    public RealParcelSourceReadRepository(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CadastralSourceMetadataDto?> GetByParcelIdAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        var results = await GetByParcelIdsAsync([landParcelId], cancellationToken);
        return results.TryGetValue(landParcelId, out var metadata) ? metadata : null;
    }

    public async Task<IReadOnlyDictionary<Guid, CadastralSourceMetadataDto>> GetByParcelIdsAsync(
        IReadOnlyCollection<Guid> landParcelIds,
        CancellationToken cancellationToken = default)
    {
        if (landParcelIds.Count == 0)
        {
            return new Dictionary<Guid, CadastralSourceMetadataDto>();
        }

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    "LandParcelId",
                    "SourceName",
                    "SourceLayer",
                    "SourceFeatureId",
                    "LandUseCode",
                    "LandUseEnglishLabel",
                    "LandUseDescription",
                    "ClaimantCategoryCode",
                    "ClaimantCategoryEnglishLabel",
                    "ParcelStatus",
                    "GnIndex",
                    "CoordinateStatus",
                    "ImportedAtUtc",
                    "ResearchRecordId",
                    "MapMarkerLatitude",
                    "MapMarkerLongitude"
                FROM land_intelligence.real_parcel_sources
                WHERE "LandParcelId" = ANY(@parcel_ids)
                """;

            var parameter = new NpgsqlParameter("parcel_ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid)
            {
                Value = landParcelIds.ToArray()
            };
            command.Parameters.Add(parameter);

            var results = new Dictionary<Guid, CadastralSourceMetadataDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var record = ReadRecord(reader);
                results[record.LandParcelId] = RealParcelSourceMetadataMapper.ToPublicDto(record);
            }

            return results;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable
                                            || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            return await GetByParcelIdsLegacyAsync(landParcelIds, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SourceReportedLandUseOptionDto>> ListSourceReportedLandUseOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _dbContext.RealParcelSources
                .AsNoTracking()
                .Where(source => source.LandUseCode != null)
                .GroupBy(source => new { source.LandUseCode, source.LandUseEnglishLabel })
                .Select(g => new { 
                    g.Key.LandUseCode, 
                    g.Key.LandUseEnglishLabel, 
                    ParcelCount = g.Count() 
                })
                .OrderBy(source => source.LandUseCode)
                .ToListAsync(cancellationToken);

            return rows
                .Select(source => new SourceReportedLandUseOptionDto(
                    source.LandUseCode!,
                    source.LandUseEnglishLabel
                        ?? ColomboCadastreSourceDomains.FormatUnknownLandUseCode(source.LandUseCode!),
                    source.ParcelCount))
                .ToList();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable
                                            || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            return [];
        }
    }

    private async Task<IReadOnlyDictionary<Guid, CadastralSourceMetadataDto>> GetByParcelIdsLegacyAsync(
        IReadOnlyCollection<Guid> landParcelIds,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                "LandParcelId",
                "SourceName",
                "SourceLayer",
                "SourceFeatureId",
                "LandUseCode",
                "LandUseDescription",
                "GnIndex",
                "CoordinateStatus",
                "ImportedAtUtc"
            FROM land_intelligence.real_parcel_sources
            WHERE "LandParcelId" = ANY(@parcel_ids)
            """;

        var parameter = new NpgsqlParameter("parcel_ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = landParcelIds.ToArray()
        };
        command.Parameters.Add(parameter);

        var results = new Dictionary<Guid, CadastralSourceMetadataDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.IsDBNull(4) ? null : reader.GetString(4);
            var text = reader.IsDBNull(5) ? null : reader.GetString(5);
            var english = ColomboCadastreSourceDomains.TryDecodeLandUseEnglishLabel(code);
            var record = new RealParcelSourceRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                code,
                english,
                text,
                null,
                null,
                null,
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetFieldValue<DateTimeOffset>(8));
            results[record.LandParcelId] = RealParcelSourceMetadataMapper.ToPublicDto(record);
        }

        return results;
    }

    private static RealParcelSourceRecord ReadRecord(DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetFieldValue<DateTimeOffset>(12),
            reader.FieldCount > 13 && !reader.IsDBNull(13) ? reader.GetString(13) : null,
            reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetDouble(14) : null,
            reader.FieldCount > 15 && !reader.IsDBNull(15) ? reader.GetDouble(15) : null);
}
