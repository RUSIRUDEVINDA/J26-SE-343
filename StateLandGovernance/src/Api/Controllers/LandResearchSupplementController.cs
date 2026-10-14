using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.Api.Controllers;

/// <summary>Research supplement classifications, separate from source cadastral land use.</summary>
[ApiController]
[Route("api/v1/land/parcels/research-supplement")]
public sealed class LandResearchSupplementController(LandIntelligenceDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResearchSupplementParcelResponse>>> ListAsync(
        CancellationToken cancellationToken)
    {
        var results = new List<ResearchSupplementParcelResponse>();
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT cover."LandParcelId", cover."ResearchRecordId", cover."SupplementGroup",
                       cover."ClassificationBasis", cover."OriginalLandUseLabel",
                       cover."GisOverlapPercentages"::text, cover."LiveVerifiedAtUtc"
                FROM land_intelligence.research_supplement_land_cover cover
                JOIN land_intelligence.land_parcels parcel ON parcel."Id" = cover."LandParcelId"
                WHERE parcel."District" = 'Colombo'
                ORDER BY cover."SupplementGroup", cover."ResearchRecordId";
                """;
            // The sidecar is created only by the isolated supplement importer.
            await using var exists = database.Database.GetDbConnection().CreateCommand();
            exists.CommandText = "SELECT to_regclass('land_intelligence.research_supplement_land_cover') IS NOT NULL";
            if (!Convert.ToBoolean(await exists.ExecuteScalarAsync(cancellationToken)))
                return Ok(results);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var group = reader.GetString(2);
                var percentages = JsonSerializer.Deserialize<Dictionary<string, double>>(reader.GetString(5)) ?? [];
                var mappedKey = group == "Scrub / low jungle" ? "Scrub" : group;
                results.Add(new ResearchSupplementParcelResponse(
                    reader.GetGuid(0), reader.GetString(1), group, reader.GetString(3),
                    reader.GetString(4), percentages.GetValueOrDefault(mappedKey),
                    reader.GetFieldValue<DateTimeOffset>(6),
                    "Source-reported State; official ownership and lease availability are not verified."));
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
        return Ok(results);
    }
}

public sealed record ResearchSupplementParcelResponse(
    Guid ParcelId,
    string RecordId,
    string SupplementGroup,
    string ClassificationBasis,
    string OriginalLandUseLabel,
    double GisOverlapPercent,
    DateTimeOffset LiveVerifiedAtUtc,
    string AvailabilityNotice);
