using System.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;

public sealed class AdministrativeLocationVerificationService : IAdministrativeLocationVerificationService
{
    private const int ProvinceBoundaryType = 1;
    private const int DistrictBoundaryType = 2;
    private const string OfficialParcelRecordSource = "Land Commissioner";
    private const string DerivedDetectionSource = "GIS administrative boundary spatial query";

    private readonly LandIntelligenceDbContext _dbContext;

    public AdministrativeLocationVerificationService(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdministrativeLocationVerificationResult> VerifyAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var parcel = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(entity => entity.Id == parcelId)
            .Select(entity => new ParcelVerificationSnapshot(
                entity.Id,
                entity.Province,
                entity.District,
                entity.Centroid,
                entity.Boundary))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Land parcel '{parcelId}' was not found.");

        var verifiedAt = DateTimeOffset.UtcNow;
        var storedProvinceProvenance = AttributeProvenanceMapper.ToDto(
            AttributeProvenance.Official(OfficialParcelRecordSource, verified: true))!;
        var storedDistrictProvenance = AttributeProvenanceMapper.ToDto(
            AttributeProvenance.Official(OfficialParcelRecordSource, verified: true))!;
        var boundarySourceProvenance = new AttributeProvenanceDto(
            AttributeProvenanceSourceType.ExternalAuthoritative,
            GisReferenceDataPaths.SourceName,
            Confidence: 1m,
            CollectedAt: verifiedAt,
            Verified: true);

        var evidence = new List<string>
        {
            $"Stored province '{parcel.Province}' and district '{parcel.District}' remain official ({OfficialParcelRecordSource}).",
            $"GIS administrative boundaries sourced from {GisReferenceDataPaths.SourceName} " +
            $"({GisReferenceDataPaths.DistrictBoundariesLayer}, {GisReferenceDataPaths.ProvinceBoundariesLayer})."
        };

        var geometrySelection = SelectParcelGeometry(parcel.Boundary, parcel.Centroid);
        if (geometrySelection is null)
        {
            evidence.Add("Parcel geometry unavailable: no boundary or centroid could be used for verification.");

            return BuildResult(
                parcel,
                geometryBasis: null,
                detectedDistrict: null,
                detectedProvince: null,
                districtMatches: null,
                provinceMatches: null,
                status: AdministrativeLocationVerificationStatus.Unavailable,
                evidence,
                storedProvinceProvenance,
                storedDistrictProvenance,
                detectedProvinceProvenance: null,
                detectedDistrictProvenance: null,
                boundarySourceProvenance);
        }

        var (parcelGeometry, geometryBasis) = geometrySelection.Value;
        evidence.Add(
            geometryBasis == AdministrativeLocationGeometryBasis.Boundary
                ? "Verification geometry basis: parcel boundary (preferred)."
                : "Verification geometry basis: parcel centroid (boundary unavailable).");

        var detectedDistrict = await DetectAdministrativeNameAsync(
            parcelGeometry,
            geometryBasis,
            DistrictBoundaryType,
            cancellationToken);
        var detectedProvince = await DetectAdministrativeNameAsync(
            parcelGeometry,
            geometryBasis,
            ProvinceBoundaryType,
            cancellationToken);

        if (detectedDistrict is not null)
        {
            evidence.Add(
                $"Detected district '{detectedDistrict}' from {QualifiedTable()} " +
                $"(BoundaryType=District) using {DescribeSpatialPredicate(geometryBasis)}.");
        }
        else
        {
            evidence.Add(
                "No imported district boundary covers the parcel geometry; GIS pilot coverage does not include this location.");
        }

        if (detectedProvince is not null)
        {
            evidence.Add(
                $"Detected province '{detectedProvince}' from {QualifiedTable()} " +
                $"(BoundaryType=Province) using {DescribeSpatialPredicate(geometryBasis)}.");
        }
        else
        {
            evidence.Add(
                "No imported province boundary covers the parcel geometry; GIS pilot coverage does not include this location.");
        }

        var (districtMatches, provinceMatches) = AdministrativeLocationVerificationEvaluator.CompareLocations(
            parcel.District,
            parcel.Province,
            detectedDistrict,
            detectedProvince);

        var status = AdministrativeLocationVerificationEvaluator.ResolveStatus(
            detectedDistrict,
            detectedProvince,
            districtMatches == true,
            provinceMatches == true);

        AttributeProvenanceDto? detectedDistrictProvenance = null;
        AttributeProvenanceDto? detectedProvinceProvenance = null;

        if (detectedDistrict is not null)
        {
            detectedDistrictProvenance = AttributeProvenanceMapper.ToDto(
                AttributeProvenance.Derived(DerivedDetectionSource, confidence: 1m, collectedAt: verifiedAt));
            evidence.Add(
                districtMatches == true
                    ? $"District match after normalization: stored '{parcel.District}' equals detected '{detectedDistrict}'."
                    : $"District mismatch after normalization: stored '{parcel.District}' vs detected '{detectedDistrict}'.");
        }

        if (detectedProvince is not null)
        {
            detectedProvinceProvenance = AttributeProvenanceMapper.ToDto(
                AttributeProvenance.Derived(DerivedDetectionSource, confidence: 1m, collectedAt: verifiedAt));
            evidence.Add(
                provinceMatches == true
                    ? $"Province match after normalization: stored '{parcel.Province}' equals detected '{detectedProvince}'."
                    : $"Province mismatch after normalization: stored '{parcel.Province}' vs detected '{detectedProvince}'.");
        }

        evidence.Add($"Verification status: {status}.");

        return BuildResult(
            parcel,
            geometryBasis,
            detectedDistrict,
            detectedProvince,
            districtMatches,
            provinceMatches,
            status,
            evidence,
            storedProvinceProvenance,
            storedDistrictProvenance,
            detectedProvinceProvenance,
            detectedDistrictProvenance,
            boundarySourceProvenance);
    }

    internal static (Geometry Geometry, AdministrativeLocationGeometryBasis Basis)? SelectParcelGeometry(
        MultiPolygon? boundary,
        Point centroid)
    {
        if (boundary is not null && !boundary.IsEmpty)
        {
            return (boundary, AdministrativeLocationGeometryBasis.Boundary);
        }

        if (centroid is not null && !centroid.IsEmpty)
        {
            return (centroid, AdministrativeLocationGeometryBasis.Centroid);
        }

        return null;
    }

    private async Task<string?> DetectAdministrativeNameAsync(
        Geometry parcelGeometry,
        AdministrativeLocationGeometryBasis geometryBasis,
        int boundaryType,
        CancellationToken cancellationToken)
    {
        var geometryWkt = parcelGeometry.AsText();

        var sql = geometryBasis == AdministrativeLocationGeometryBasis.Centroid
            ? $"""
               SELECT "Name"
               FROM {QualifiedTable()}
               WHERE "BoundaryType" = @boundaryType
                 AND ST_Covers("Boundary", ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326))
               LIMIT 1
               """
            : $"""
               SELECT "Name"
               FROM {QualifiedTable()}
               WHERE "BoundaryType" = @boundaryType
                 AND ST_Intersects("Boundary", ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326))
               ORDER BY ST_Area(ST_Intersection("Boundary", ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326))) DESC NULLS LAST
               LIMIT 1
               """;

        return await ExecuteScalarStringAsync(
            sql,
            cancellationToken,
            ("boundaryType", boundaryType),
            ("geometryWkt", geometryWkt));
    }

    private static AdministrativeLocationVerificationResult BuildResult(
        ParcelVerificationSnapshot parcel,
        AdministrativeLocationGeometryBasis? geometryBasis,
        string? detectedDistrict,
        string? detectedProvince,
        bool? districtMatches,
        bool? provinceMatches,
        AdministrativeLocationVerificationStatus status,
        IReadOnlyList<string> evidence,
        AttributeProvenanceDto storedProvinceProvenance,
        AttributeProvenanceDto storedDistrictProvenance,
        AttributeProvenanceDto? detectedProvinceProvenance,
        AttributeProvenanceDto? detectedDistrictProvenance,
        AttributeProvenanceDto boundarySourceProvenance) =>
        new()
        {
            ParcelId = parcel.Id,
            StoredProvince = parcel.Province,
            DetectedProvince = detectedProvince,
            ProvinceMatches = provinceMatches,
            StoredDistrict = parcel.District,
            DetectedDistrict = detectedDistrict,
            DistrictMatches = districtMatches,
            GeometryBasis = geometryBasis,
            SourceName = GisReferenceDataPaths.SourceName,
            SourceLayer = GisReferenceDataPaths.DistrictBoundariesLayer,
            Status = status,
            Evidence = evidence,
            StoredProvinceProvenance = storedProvinceProvenance,
            StoredDistrictProvenance = storedDistrictProvenance,
            DetectedProvinceProvenance = detectedProvinceProvenance,
            DetectedDistrictProvenance = detectedDistrictProvenance,
            BoundarySourceProvenance = boundarySourceProvenance
        };

    private async Task<string?> ExecuteScalarStringAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            AddParameter(command, name, value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : result.ToString();
    }

    private static string QualifiedTable() =>
        $"{LandIntelligenceDbContext.SchemaName}.gis_administrative_boundaries";

    private static string DescribeSpatialPredicate(AdministrativeLocationGeometryBasis geometryBasis) =>
        geometryBasis == AdministrativeLocationGeometryBasis.Centroid
            ? "ST_Covers (point-in-polygon)"
            : "ST_Intersects with maximum ST_Area(ST_Intersection) overlap";

    private static async Task EnsureConnectionOpenAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ParcelVerificationSnapshot(
        Guid Id,
        string Province,
        string District,
        Point Centroid,
        MultiPolygon? Boundary);
}
