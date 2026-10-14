using NetTopologySuite.Features;
using NetTopologySuite.IO;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

internal static class GeoJsonFeatureCollectionReader
{
    private static readonly GeoJsonReader Reader = new();

    public static FeatureCollection Read(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var featureCollection = Reader.Read<FeatureCollection>(json);

        return featureCollection ?? throw new InvalidDataException(
            $"GeoJSON file '{filePath}' did not contain a FeatureCollection.");
    }
}
