using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using Xunit.Abstractions;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

[Trait("Category", "Integration")]
[Trait("Component", "LandIntelligence")]
public sealed class GisReferenceDataValidationIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private IGisReferenceDataImportService? _importService;
    private IGisReferenceDataValidationService? _validationService;

    public GisReferenceDataValidationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        var configuration = LandIntelligenceIntegrationConfiguration.LoadApiConfiguration();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLandIntelligenceInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _importService = _serviceProvider.GetRequiredService<IGisReferenceDataImportService>();
        _validationService = _serviceProvider.GetRequiredService<IGisReferenceDataValidationService>();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidateHambantotaPilotAsync_passes_postgis_checks_for_imported_reference_data()
    {
        Assert.NotNull(_importService);
        Assert.NotNull(_validationService);

        await _importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));

        var result = await _validationService.ValidateHambantotaPilotAsync();

        WriteValidationSummary(result);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.True(result.TableCounts.Values.All(count => count >= 0));

        foreach (var summary in result.TableSummaries.Values)
        {
            Assert.Equal(0, summary.NullGeometryCount);
            Assert.Equal(0, summary.WrongSridCount);
            Assert.Equal(0, summary.InvalidGeometryCount);
            Assert.Equal(0, summary.UnexpectedGeometryTypeCount);
            Assert.True(summary.GistIndexExists);
        }

        var spatial = result.HambantotaSpatialValidation;
        Assert.True(spatial.HambantotaDistrictExists);
        Assert.True(spatial.SouthernProvinceExists);
        Assert.True(spatial.DistrictIntersectsProvince);
        Assert.Equal(0, spatial.RoadsOutsideHambantotaCount);
        Assert.Equal(0, spatial.WaterFeaturesOutsideHambantotaCount);
        Assert.Equal(0, spatial.SoilGroupsOutsideHambantotaCount);
        Assert.Equal(0, spatial.SoilConservationAreasOutsideHambantotaCount);
        Assert.True(spatial.ExpresswaysIntersectingHambantotaCount > 0);
        Assert.True(spatial.WaterFeaturesIntersectingHambantotaCount > 0);
        Assert.True(spatial.SoilGroupsIntersectingHambantotaCount > 0);
        Assert.True(spatial.SoilConservationAreasIntersectingHambantotaCount > 0);

        Assert.True(result.NearestRoadDistance.RoadFound);
        Assert.NotNull(result.NearestRoadDistance.DistanceMeters);
        Assert.True(result.NearestRoadDistance.DistanceMeters >= 0);
    }

    [Fact]
    public async Task ValidateHambantotaPilotAsync_reports_expected_geometry_types_and_counts()
    {
        Assert.NotNull(_importService);
        Assert.NotNull(_validationService);

        await _importService.ImportHambantotaPilotAsync(GisReferenceDataPaths.ResolveDataRoot(null));
        var result = await _validationService.ValidateHambantotaPilotAsync();

        Assert.True(result.TableCounts["gis_administrative_boundaries"] >= 2);
        Assert.True(result.TableCounts["gis_roads"] > 0);
        Assert.True(result.TableCounts["gis_water_features"] > 0);
        Assert.True(result.TableCounts["gis_soil_groups"] > 0);
        Assert.True(result.TableCounts["gis_soil_conservation_areas"] > 0);
        Assert.True(result.TableCounts["gis_soil_erosion_observations"] >= 0);

        Assert.Equal("ST_MultiPolygon", result.TableSummaries["gis_administrative_boundaries"].DominantGeometryType);
        Assert.Equal("ST_MultiLineString", result.TableSummaries["gis_roads"].DominantGeometryType);
        Assert.Equal("ST_MultiPolygon", result.TableSummaries["gis_soil_groups"].DominantGeometryType);
        Assert.Equal("ST_MultiPolygon", result.TableSummaries["gis_soil_conservation_areas"].DominantGeometryType);
    }

    private void WriteValidationSummary(GisReferenceDataValidationResult result)
    {
        _output.WriteLine($"IsValid={result.IsValid}");
        foreach (var entry in result.TableCounts.OrderBy(pair => pair.Key))
        {
            var summary = result.TableSummaries[entry.Key];
            _output.WriteLine(
                $"{entry.Key}={entry.Value}, type={summary.DominantGeometryType}, gist={summary.GistIndexExists}");
        }

        _output.WriteLine(
            $"NearestRoad={result.NearestRoadDistance.NearestRoadName}, distanceMeters={result.NearestRoadDistance.DistanceMeters}");
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }
}
