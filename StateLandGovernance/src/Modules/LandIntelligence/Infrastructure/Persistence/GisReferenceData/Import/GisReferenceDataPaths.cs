namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

internal static class GisReferenceDataPaths
{
    public const string SourceName = "LandIntelligence_GIS";

    public const string DistrictBoundariesLayer = "district_boundaries";

    public const string ProvinceBoundariesLayer = "province_boundaries";

    public const string ExpresswaysLayer = "expressways";

    public const string CanalsLayer = "canals";

    public const string LakesLayer = "lakes";

    public const string SoilGroupsLayer = "soil_groups";

    public const string SoilConservationAreasLayer = "soil_conservation_areas";

    public const string SoilErosionLayer = "soil_erosion";

    public const string HambantotaDistrictName = "Hambantota";

    public static string ResolveDataRoot(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var fullPath = Path.GetFullPath(overridePath);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"GIS data root was not found at '{fullPath}'.");
            }

            return fullPath;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "StateLandGovernance", "data", "gis", SourceName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(directory.FullName, "data", "gis", SourceName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"GIS data root '{SourceName}' was not found. Pass dataRootPath explicitly.");
    }

    public static string ResolveDatasetPath(string dataRoot, string relativePath) =>
        Path.Combine(dataRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
