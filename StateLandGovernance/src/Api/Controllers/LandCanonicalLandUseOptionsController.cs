using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.Api.Controllers;

/// <summary>
/// Canonical land-use filter options for Find Suitable Land. Original cadastral labels and the
/// project GIS supplement stay in their own stores; this endpoint only reports distinct-parcel counts.
/// </summary>
[ApiController]
[Route("api/v1/land/parcels/canonical-land-use-options")]
public sealed class LandCanonicalLandUseOptionsController(LandIntelligenceDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CanonicalLandUseOptionResponse>>> ListAsync(
        [FromQuery] string? district,
        CancellationToken cancellationToken)
    {
        var districtFilter = string.IsNullOrWhiteSpace(district) ? null : district.Trim();

        var parcels = database.LandParcels
            .AsNoTracking()
            .Where(parcel => !EF.Functions.ILike(parcel.CadastralNumber, "DEMO-%"));
        if (districtFilter is not null)
        {
            parcels = parcels.Where(parcel => EF.Functions.ILike(parcel.District, districtFilter));
        }

        var evidence = new List<CanonicalLandUseParcelEvidence>();

        var sourceRows = await database.RealParcelSources
            .AsNoTracking()
            .Join(parcels, source => source.LandParcelId, parcel => parcel.Id,
                (source, parcel) => new { parcel.Id, source.LandUseCode, source.LandUseEnglishLabel })
            .ToListAsync(cancellationToken);
        evidence.AddRange(sourceRows.Select(row =>
            new CanonicalLandUseParcelEvidence(row.Id, row.LandUseCode, row.LandUseEnglishLabel, null)));

        var parcelIdsInScope = districtFilter is null
            ? null
            : (await parcels.Select(parcel => parcel.Id).ToListAsync(cancellationToken)).ToHashSet();
        evidence.AddRange(await ReadGisSupplementAsync(parcelIdsInScope, cancellationToken));

        var counts = CanonicalLandUseCatalog.CountDistinctParcels(evidence);
        return Ok(counts.Select(count => new CanonicalLandUseOptionResponse(
            count.Key,
            count.Label,
            count.ParcelCount,
            count.SourceMatchedParcelCount,
            count.GisMatchedParcelCount,
            count.BothSourcesParcelCount)).ToList());
    }

    private async Task<IReadOnlyList<CanonicalLandUseParcelEvidence>> ReadGisSupplementAsync(
        HashSet<Guid>? parcelIdsInScope,
        CancellationToken cancellationToken)
    {
        var rows = new List<CanonicalLandUseParcelEvidence>();
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // The sidecar is created only by the isolated supplement importer.
            await using var exists = database.Database.GetDbConnection().CreateCommand();
            exists.CommandText = "SELECT to_regclass('land_intelligence.research_supplement_land_cover') IS NOT NULL";
            if (!Convert.ToBoolean(await exists.ExecuteScalarAsync(cancellationToken)))
            {
                return rows;
            }

            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT "LandParcelId", "SupplementGroup"
                FROM land_intelligence.research_supplement_land_cover;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var parcelId = reader.GetGuid(0);
                if (parcelIdsInScope is not null && !parcelIdsInScope.Contains(parcelId))
                {
                    continue;
                }

                rows.Add(new CanonicalLandUseParcelEvidence(parcelId, null, null, reader.GetString(1)));
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }

        return rows;
    }
}

public sealed record CanonicalLandUseOptionResponse(
    string Key,
    string Label,
    int ParcelCount,
    int SourceMatchedParcelCount,
    int GisMatchedParcelCount,
    int BothSourcesParcelCount);
