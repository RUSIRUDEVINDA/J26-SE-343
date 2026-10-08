using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Parcels;

internal static class LandParcelCommandTestData
{
    public static GeoJsonPolygonDto SampleBoundaryPolygon => new()
    {
        Type = "Polygon",
        Coordinates =
        [
            new List<IReadOnlyList<double>>
            {
                new List<double> { 79.8600, 6.9260 },
                new List<double> { 79.8620, 6.9260 },
                new List<double> { 79.8620, 6.9280 },
                new List<double> { 79.8600, 6.9280 },
                new List<double> { 79.8600, 6.9260 }
            }
        ]
    };

    public static CreateLandParcelCommand CreateValidCreateCommand(string cadastralNumber = "SYNTH-CREATE-001") =>
        new()
        {
            CadastralNumber = cadastralNumber,
            SurveyPlanReference = "SYNTH-SURVEY-001",
            CategoryType = LandCategoryType.StateLand,
            CategoryDescription = "[SYNTHETIC] Test parcel",
            AreaValue = 2.5m,
            AreaUnit = AreaUnit.Hectares,
            Province = "Western",
            District = "Colombo",
            DivisionalSecretariat = "Colombo DS",
            GramaNiladhariDivision = "GN-Test",
            CentroidLatitude = 6.9271,
            CentroidLongitude = 79.8612,
            CurrentUseType = LandUseType.Agricultural,
            CurrentUseDescription = "[SYNTHETIC] Agricultural use",
            Characteristics = new LandCharacteristicsInputDto
            {
                SoilType = "Red Yellow Latosol",
                TerrainDescription = "Flat terrain",
                ElevationMeters = 12m
            }
        };

    public static CreateLandParcelCommand CreateExtendedCreateCommand(string cadastralNumber = "SYNTH-EXTENDED-001") =>
        CreateValidCreateCommand(cadastralNumber) with
        {
            BoundaryPolygon = SampleBoundaryPolygon,
            Characteristics = new LandCharacteristicsInputDto
            {
                SoilType = "Loam",
                TerrainDescription = "Gently sloping",
                ElevationMeters = 25m,
                SoilTypeProvenance = new AttributeProvenanceDto(
                    AttributeProvenanceSourceType.Official,
                    "Survey Department",
                    1m,
                    null,
                    true)
            },
            SpatialConstraints =
            [
                new SpatialConstraintInputDto
                {
                    Type = SpatialConstraintType.Setback,
                    Description = "[SYNTHETIC] Setback zone",
                    Severity = RestrictionSeverity.Medium,
                    Geometry = SampleBoundaryPolygon
                }
            ],
            EnvironmentalRestrictions =
            [
                new EnvironmentalRestrictionInputDto
                {
                    Type = EnvironmentalRestrictionType.Wetland,
                    Description = "[SYNTHETIC] Wetland buffer",
                    Severity = RestrictionSeverity.High,
                    DataProvenance = new AttributeProvenanceDto(
                        AttributeProvenanceSourceType.Derived,
                        "GIS overlay",
                        0.9m,
                        null,
                        false)
                }
            ],
            InfrastructureFeatures =
            [
                new InfrastructureFeatureInputDto
                {
                    Type = InfrastructureFeatureType.Road,
                    Name = "[SYNTHETIC] Access Road",
                    DistanceMeters = 250m,
                    LocationLatitude = 6.9275,
                    LocationLongitude = 79.8615,
                    DistanceProvenance = new AttributeProvenanceDto(
                        AttributeProvenanceSourceType.Derived,
                        "PostGIS nearest-road",
                        0.95m,
                        null,
                        false)
                }
            ],
            RegulatoryReferences =
            [
                new RegulatoryReferenceInputDto
                {
                    GazetteNumber = "SYNTH-GZ-001",
                    Title = "[SYNTHETIC] Land use gazette",
                    EffectiveDate = new DateOnly(2026, 1, 1),
                    Summary = "[SYNTHETIC] Regulatory summary",
                    DataProvenance = new AttributeProvenanceDto(
                        AttributeProvenanceSourceType.Official,
                        "Government Gazette",
                        1m,
                        null,
                        true)
                }
            ]
        };
}
