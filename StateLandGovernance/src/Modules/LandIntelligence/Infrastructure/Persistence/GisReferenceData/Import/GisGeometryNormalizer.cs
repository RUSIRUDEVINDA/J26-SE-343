using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;

internal static class GisGeometryNormalizer
{
    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGIS.PostGisConfiguration.DefaultSpatialReferenceSystemId);

    public static MultiPolygon? ToMultiPolygon(Geometry? geometry)
    {
        geometry = PrepareGeometry(geometry);
        if (geometry is null)
        {
            return null;
        }

        return geometry switch
        {
            MultiPolygon multiPolygon => multiPolygon,
            Polygon polygon => Factory.CreateMultiPolygon(new[] { polygon }),
            _ => null
        };
    }

    public static MultiLineString? ToMultiLineString(Geometry? geometry)
    {
        geometry = PrepareGeometry(geometry);
        if (geometry is null)
        {
            return null;
        }

        return geometry switch
        {
            MultiLineString multiLineString => multiLineString,
            LineString lineString => Factory.CreateMultiLineString(new[] { lineString }),
            _ => null
        };
    }

    public static Point? ToPoint(Geometry? geometry)
    {
        geometry = PrepareGeometry(geometry);
        return geometry as Point;
    }

    public static Geometry? ToWaterGeometry(Geometry? geometry)
    {
        geometry = PrepareGeometry(geometry);
        if (geometry is null)
        {
            return null;
        }

        return geometry switch
        {
            Polygon or MultiPolygon or LineString or MultiLineString => geometry,
            GeometryCollection collection when collection.NumGeometries > 0 =>
                PrepareGeometry(collection.GetGeometryN(0)),
            _ => null
        };
    }

    public static bool IsSpatiallyRelevant(Geometry featureGeometry, Geometry pilotArea) =>
        PrepareGeometry(featureGeometry) is Geometry prepared
        && PrepareGeometry(pilotArea) is Geometry pilot
        && prepared.Intersects(pilot);

    public static Geometry? PrepareGeometry(Geometry? geometry)
    {
        if (geometry is null || geometry.IsEmpty)
        {
            return null;
        }

        var copy = geometry.Copy();
        copy.SRID = PostGIS.PostGisConfiguration.DefaultSpatialReferenceSystemId;

        return copy.IsValid ? copy : null;
    }

    public static string DescribeGeometry(Geometry? geometry) =>
        geometry is null ? "null" : $"{geometry.GeometryType} SRID {geometry.SRID}";
}

internal static class GisReferenceImportIdentityResolver
{
    public static (string? SourceFeatureId, string? SourceFingerprint) Resolve(
        IFeature feature,
        string sourceName,
        string sourceLayer,
        string fingerprintSeed)
    {
        var sourceFeatureId = ResolveSourceFeatureId(feature);
        if (!string.IsNullOrWhiteSpace(sourceFeatureId))
        {
            return (sourceFeatureId, null);
        }

        return (null, ComputeFingerprint(sourceName, sourceLayer, fingerprintSeed));
    }

    public static string? ResolveSourceFeatureId(IFeature feature)
    {
        var objectId = ReadAttribute(feature, "objectid");
        if (!string.IsNullOrWhiteSpace(objectId))
        {
            return objectId;
        }

        return ReadAttribute(feature, "id");
    }

    public static string ComputeFingerprint(string sourceName, string sourceLayer, string fingerprintSeed)
    {
        var payload = $"{sourceName}|{sourceLayer}|{fingerprintSeed}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string? ReadAttribute(IFeature feature, string attributeName)
    {
        if (feature.Attributes is not AttributesTable attributes || !attributes.Exists(attributeName))
        {
            return null;
        }

        return ConvertAttributeValue(attributes[attributeName]);
    }

    private static string? ConvertAttributeValue(object? value)
    {
        return value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()?.Trim()
        };
    }
}

internal static class GisReferenceAttributeReader
{
    public static string? ReadString(IFeature feature, string attributeName) =>
        GisReferenceImportIdentityResolver.ReadAttribute(feature, attributeName);

    public static decimal? ReadDecimal(IFeature feature, string attributeName)
    {
        var text = ReadString(feature, attributeName);
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
