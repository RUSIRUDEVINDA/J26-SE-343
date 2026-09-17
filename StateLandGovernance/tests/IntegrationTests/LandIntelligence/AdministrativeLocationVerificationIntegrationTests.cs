using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using Xunit.Abstractions;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class AdministrativeLocationVerificationIntegrationTests : IAsyncLifetime
{
    private const double OutsideCoverageLatitude = 6.9271;
    private const double OutsideCoverageLongitude = 79.8612;

    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private ILandParcelRepository? _repository;
    private IAdministrativeLocationVerificationService? _verificationService;
    private LandIntelligenceDbContext? _dbContext;
    private double _hambantotaLatitude;
    private double _hambantotaLongitude;
    private readonly List<Guid> _createdParcelIds = [];

    public AdministrativeLocationVerificationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _repository = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        _verificationService = _serviceProvider.GetRequiredService<IAdministrativeLocationVerificationService>();
        _dbContext = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();

        var importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        await importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        (_hambantotaLatitude, _hambantotaLongitude) = await ReadHambantotaInteriorPointAsync();
        _output.WriteLine(
            $"Hambantota interior test point: lat={_hambantotaLatitude}, lon={_hambantotaLongitude}");
    }

    [Fact]
    public async Task VerifyAsync_detects_Hambantota_and_Southern_for_centroid_inside_pilot_coverage()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Southern Province",
            "Hambantota",
            _hambantotaLatitude,
            _hambantotaLongitude);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        WriteResult(result);

        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, result.Status);
        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, result.GeometryBasis);
        Assert.Equal("Hambantota", result.DetectedDistrict);
        Assert.Equal("Southern", result.DetectedProvince);
        Assert.True(result.DistrictMatches);
        Assert.True(result.ProvinceMatches);
    }

    [Fact]
    public async Task VerifyAsync_normalizes_Southern_Province_against_detected_Southern()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Southern Province",
            "Hambantota",
            _hambantotaLatitude,
            _hambantotaLongitude);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, result.Status);
        Assert.True(result.ProvinceMatches);
        Assert.True(result.DistrictMatches);
    }

    [Fact]
    public async Task VerifyAsync_reports_Mismatch_for_incorrect_stored_district_inside_Hambantota()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Southern Province",
            "Matara",
            _hambantotaLatitude,
            _hambantotaLongitude);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationVerificationStatus.Mismatch, result.Status);
        Assert.Equal("Hambantota", result.DetectedDistrict);
        Assert.False(result.DistrictMatches);
        Assert.True(result.ProvinceMatches);
    }

    [Fact]
    public async Task VerifyAsync_reports_Mismatch_for_incorrect_stored_province_inside_Southern()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Western Province",
            "Hambantota",
            _hambantotaLatitude,
            _hambantotaLongitude);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationVerificationStatus.Mismatch, result.Status);
        Assert.Equal("Southern", result.DetectedProvince);
        Assert.False(result.ProvinceMatches);
        Assert.True(result.DistrictMatches);
    }

    [Fact]
    public async Task VerifyAsync_returns_Unavailable_for_parcel_outside_imported_gis_coverage()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Central Province",
            "Kandy",
            OutsideCoverageLatitude,
            OutsideCoverageLongitude);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationVerificationStatus.Unavailable, result.Status);
        Assert.Null(result.DetectedDistrict);
        Assert.Null(result.DetectedProvince);
        Assert.Null(result.DistrictMatches);
        Assert.Null(result.ProvinceMatches);
    }

    [Fact]
    public async Task VerifyAsync_prefers_boundary_geometry_when_available()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var boundary = CreateSmallBoundaryAround(_hambantotaLatitude, _hambantotaLongitude, delta: 0.002);
        var parcel = await PersistParcelAsync(
            "Southern Province",
            "Hambantota",
            _hambantotaLatitude,
            _hambantotaLongitude,
            boundary);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationGeometryBasis.Boundary, result.GeometryBasis);
        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_uses_centroid_when_boundary_is_unavailable()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Southern Province",
            "Hambantota",
            _hambantotaLatitude,
            _hambantotaLongitude,
            boundary: null);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(AdministrativeLocationGeometryBasis.Centroid, result.GeometryBasis);
        Assert.Equal(AdministrativeLocationVerificationStatus.Verified, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_is_deterministic_for_repeated_calls()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Southern Province",
            "Hambantota",
            _hambantotaLatitude,
            _hambantotaLongitude);

        var first = await _verificationService.VerifyAsync(parcel.Id);
        var second = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.DetectedDistrict, second.DetectedDistrict);
        Assert.Equal(first.DetectedProvince, second.DetectedProvince);
        Assert.Equal(first.GeometryBasis, second.GeometryBasis);
        Assert.Equal(first.DistrictMatches, second.DistrictMatches);
        Assert.Equal(first.ProvinceMatches, second.ProvinceMatches);
    }

    [Fact]
    public async Task VerifyAsync_records_official_derived_and_external_provenance()
    {
        Assert.NotNull(_repository);
        Assert.NotNull(_verificationService);

        var parcel = await PersistParcelAsync(
            "Southern Province",
            "Hambantota",
            _hambantotaLatitude,
            _hambantotaLongitude);

        var result = await _verificationService.VerifyAsync(parcel.Id);

        Assert.Equal(AttributeProvenanceSourceType.Official, result.StoredDistrictProvenance.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Official, result.StoredProvinceProvenance.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Derived, result.DetectedDistrictProvenance!.SourceType);
        Assert.Equal(AttributeProvenanceSourceType.Derived, result.DetectedProvinceProvenance!.SourceType);
        Assert.Equal(
            AttributeProvenanceSourceType.ExternalAuthoritative,
            result.BoundarySourceProvenance.SourceType);
    }

    private async Task<LandParcel> PersistParcelAsync(
        string province,
        string district,
        double latitude,
        double longitude,
        GeoBoundary? boundary = null)
    {
        Assert.NotNull(_repository);

        var cadastralNumber = $"H4-VERIFY-{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "H4-VERIFY-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC] Administrative verification test parcel"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation(province, district, $"{district} DS"),
            new SpatialReference(latitude, longitude, boundary: boundary),
            null,
            null);

        await _repository.AddAsync(parcel);
        _createdParcelIds.Add(parcel.Id);
        return parcel;
    }

    private static GeoBoundary CreateSmallBoundaryAround(
        double latitude,
        double longitude,
        double delta)
    {
        return new GeoBoundary([
            new GeoCoordinate(latitude - delta, longitude - delta),
            new GeoCoordinate(latitude - delta, longitude + delta),
            new GeoCoordinate(latitude + delta, longitude + delta),
            new GeoCoordinate(latitude + delta, longitude - delta)
        ]);
    }

    private async Task<(double Latitude, double Longitude)> ReadHambantotaInteriorPointAsync()
    {
        Assert.NotNull(_dbContext);

        const string sql = """
            SELECT
                ST_Y(ST_PointOnSurface("Boundary")) AS "Latitude",
                ST_X(ST_PointOnSurface("Boundary")) AS "Longitude"
            FROM land_intelligence.gis_administrative_boundaries
            WHERE "Name" = @districtName
              AND "BoundaryType" = @districtType
            LIMIT 1
            """;

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var districtName = command.CreateParameter();
        districtName.ParameterName = "districtName";
        districtName.Value = GisReferenceDataPaths.HambantotaDistrictName;
        command.Parameters.Add(districtName);

        var districtType = command.CreateParameter();
        districtType.ParameterName = "districtType";
        districtType.Value = 2;
        command.Parameters.Add(districtType);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Hambantota district boundary was not found for integration tests.");
        }

        return (
            reader.GetDouble(reader.GetOrdinal("Latitude")),
            reader.GetDouble(reader.GetOrdinal("Longitude")));
    }

    private void WriteResult(AdministrativeLocationVerificationResult result)
    {
        _output.WriteLine(
            $"Status={result.Status}, basis={result.GeometryBasis}, district={result.DetectedDistrict}, province={result.DetectedProvince}");
        foreach (var line in result.Evidence)
        {
            _output.WriteLine(line);
        }
    }

    public async Task DisposeAsync()
    {
        if (_dbContext is not null && _createdParcelIds.Count > 0)
        {
            var parcels = await _dbContext.LandParcels
                .Where(parcel => _createdParcelIds.Contains(parcel.Id))
                .ToListAsync();

            _dbContext.LandParcels.RemoveRange(parcels);
            await _dbContext.SaveChangesAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}
