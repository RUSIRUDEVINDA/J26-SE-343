using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

/// <summary>
/// Shared district-boundary coverage checks for GIS enrichment.
/// Defaults preserve Hambantota-only pilot behavior via configuration.
/// </summary>
internal static class GisEnrichmentCoverageHelper
{
    private const int DistrictBoundaryType = 2;

    public static IReadOnlyList<string> ResolveSupportedDistricts(GisEnrichmentCoverageOptions options)
    {
        var names = options.SupportedDistrictNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return names is { Length: > 0 }
            ? names
            : [GisEnrichmentCoverageDefaults.Hambantota];
    }

    public static async Task<GisCoverageAssessment> AssessAsync(
        DbContext dbContext,
        Geometry parcelGeometry,
        AdministrativeLocationGeometryBasis geometryBasis,
        GisEnrichmentCoverageOptions options,
        CancellationToken cancellationToken)
    {
        var districts = ResolveSupportedDistricts(options);
        var geometryWkt = parcelGeometry.AsText();
        var spatialPredicate = geometryBasis == AdministrativeLocationGeometryBasis.Centroid
            ? "ST_Covers(b.\"Boundary\", ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326))"
            : "ST_Intersects(b.\"Boundary\", ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326))";

        var table = $"{LandIntelligenceDbContext.SchemaName}.gis_administrative_boundaries";
        var districtParams = districts
            .Select((_, index) => $"@districtName{index}")
            .ToArray();
        var sql = $"""
                   SELECT b."Name"
                   FROM {table} b
                   WHERE b."BoundaryType" = @districtType
                     AND b."Name" IN ({string.Join(", ", districtParams)})
                     AND {spatialPredicate}
                   LIMIT 1
                   """;

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "districtType", DistrictBoundaryType);
        AddParameter(command, "geometryWkt", geometryWkt);
        for (var i = 0; i < districts.Count; i++)
        {
            AddParameter(command, $"districtName{i}", districts[i]);
        }

        var matched = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(matched))
        {
            return GisCoverageAssessment.Outside(
                districts,
                $"Parcel is outside configured GIS enrichment coverage districts ({string.Join(", ", districts)}).");
        }

        return GisCoverageAssessment.Inside(matched, districts);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

internal sealed record GisCoverageAssessment(
    bool IsInsideCoverage,
    string? MatchedDistrict,
    IReadOnlyList<string> SupportedDistricts,
    string EvidenceMessage)
{
    public static GisCoverageAssessment Inside(string matchedDistrict, IReadOnlyList<string> supported) =>
        new(true, matchedDistrict, supported,
            $"Parcel is inside GIS enrichment coverage district '{matchedDistrict}'.");

    public static GisCoverageAssessment Outside(IReadOnlyList<string> supported, string message) =>
        new(false, null, supported, message);
}
