using Neo4j.Driver;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Mapping;

internal static class GisKnowledgeGraphMapper
{
    public static object ToOwnershipParameters(Guid parcelId) => new
    {
        parcelId = parcelId.ToString(),
        ownershipSource = GisGraphRelationshipOwnership.SourceName
    };

    public static object ToProvinceParameters(ProvinceGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        name = node.Name,
        sourceName = node.SourceName
    };

    public static object ToDistrictParameters(DistrictGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        name = node.Name,
        sourceName = node.SourceName
    };

    public static object ToRoadParameters(RoadGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        name = node.Name,
        roadType = node.RoadType,
        sourceName = node.SourceName
    };

    public static object ToWaterFeatureParameters(WaterFeatureGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        name = node.Name,
        featureType = node.FeatureType,
        sourceName = node.SourceName
    };

    public static object ToSoilGroupParameters(SoilGroupGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        name = node.Name,
        sourceName = node.SourceName
    };

    public static object ToConservationAreaParameters(ConservationAreaGraphNodeDto node) => new
    {
        id = node.Id.ToString(),
        name = node.Name,
        sourceName = node.SourceName
    };

    public static object ToAdministrativeLinkParameters(
        Guid parcelId,
        Guid targetId,
        DateTimeOffset derivedAt) => new
    {
        parcelId = parcelId.ToString(),
        targetId = targetId.ToString(),
        ownershipSource = GisGraphRelationshipOwnership.SourceName,
        derivedAt = derivedAt.UtcDateTime.ToString("O")
    };

    public static object ToRoadLinkParameters(
        Guid parcelId,
        GisDerivedRoadGraphSync road) => new
    {
        parcelId = parcelId.ToString(),
        targetId = road.RoadReferenceId.ToString(),
        distanceMeters = road.DistanceMeters,
        ownershipSource = GisGraphRelationshipOwnership.SourceName,
        derivedAt = road.DerivedAt.UtcDateTime.ToString("O")
    };

    public static object ToWaterLinkParameters(
        Guid parcelId,
        GisDerivedWaterGraphSync water) => new
    {
        parcelId = parcelId.ToString(),
        targetId = water.WaterReferenceId.ToString(),
        distanceMeters = water.DistanceMeters,
        featureType = water.FeatureType,
        ownershipSource = GisGraphRelationshipOwnership.SourceName,
        derivedAt = water.DerivedAt.UtcDateTime.ToString("O")
    };

    public static object ToSoilLinkParameters(
        Guid parcelId,
        GisDerivedSoilGraphSync soil) => new
    {
        parcelId = parcelId.ToString(),
        targetId = soil.SoilGroupReferenceId.ToString(),
        overlapPercentage = soil.OverlapPercentage,
        ownershipSource = GisGraphRelationshipOwnership.SourceName,
        derivedAt = soil.DerivedAt.UtcDateTime.ToString("O")
    };

    public static object ToConservationLinkParameters(
        Guid parcelId,
        GisDerivedConservationGraphSync conservation) => new
    {
        parcelId = parcelId.ToString(),
        targetId = conservation.ConservationAreaReferenceId.ToString(),
        overlapPercentage = conservation.OverlapPercentage,
        ownershipSource = GisGraphRelationshipOwnership.SourceName,
        derivedAt = conservation.DerivedAt.UtcDateTime.ToString("O")
    };

    public static LandParcelGisGraphIntelligenceDto? ToParcelGisGraphIntelligenceDto(IRecord record)
    {
        var parcelIdText = ReadOptionalString(record, "parcelId");
        if (parcelIdText is null)
        {
            return null;
        }

        var parcelId = Guid.Parse(parcelIdText);
        var conservationAreas = ReadConservationAreas(record);

        GisDerivedRoadGraphSync? road = null;
        GisDerivedWaterGraphSync? water = null;
        GisDerivedSoilGraphSync? soil = null;
        GisDerivedAdministrativeGraphSync? administrative = null;

        if (ReadOptionalString(record, "provinceReferenceId") is not null
            && ReadOptionalString(record, "districtReferenceId") is not null)
        {
            administrative = new GisDerivedAdministrativeGraphSync(
                Guid.Parse(ReadOptionalString(record, "provinceReferenceId")!),
                ReadOptionalString(record, "provinceName")!,
                Guid.Parse(ReadOptionalString(record, "districtReferenceId")!),
                ReadOptionalString(record, "districtName")!,
                GisGraphRelationshipOwnership.SourceName,
                ReadDerivedAt(record, "provinceDerivedAt"));
        }

        if (ReadOptionalString(record, "roadReferenceId") is not null)
        {
            road = new GisDerivedRoadGraphSync(
                Guid.Parse(ReadOptionalString(record, "roadReferenceId")!),
                ReadOptionalString(record, "roadName"),
                ReadOptionalString(record, "roadType") ?? "Unspecified",
                ReadOptionalDecimal(record, "roadDistanceMeters") ?? 0m,
                ReadOptionalString(record, "roadSource") ?? GisGraphRelationshipOwnership.SourceName,
                ReadDerivedAt(record, "roadDerivedAt"));
        }

        if (ReadOptionalString(record, "waterReferenceId") is not null)
        {
            water = new GisDerivedWaterGraphSync(
                Guid.Parse(ReadOptionalString(record, "waterReferenceId")!),
                ReadOptionalString(record, "waterFeatureName"),
                ReadOptionalString(record, "waterFeatureType") ?? "Unspecified",
                ReadOptionalDecimal(record, "waterDistanceMeters") ?? 0m,
                ReadOptionalString(record, "waterSource") ?? GisGraphRelationshipOwnership.SourceName,
                ReadDerivedAt(record, "waterDerivedAt"));
        }

        if (ReadOptionalString(record, "soilGroupReferenceId") is not null)
        {
            soil = new GisDerivedSoilGraphSync(
                Guid.Parse(ReadOptionalString(record, "soilGroupReferenceId")!),
                ReadOptionalString(record, "soilGroupName")!,
                ReadOptionalDecimal(record, "soilOverlapPercentage"),
                ReadOptionalString(record, "soilSource") ?? GisGraphRelationshipOwnership.SourceName,
                ReadDerivedAt(record, "soilDerivedAt"));
        }

        return new LandParcelGisGraphIntelligenceDto
        {
            ParcelId = parcelId,
            DetectedProvince = administrative?.ProvinceName,
            ProvinceReferenceId = administrative?.ProvinceReferenceId,
            DetectedDistrict = administrative?.DistrictName,
            DistrictReferenceId = administrative?.DistrictReferenceId,
            NearestRoad = road,
            NearestWater = water,
            DerivedSoil = soil,
            ConservationAreas = conservationAreas
        };
    }

    private static IReadOnlyList<GisDerivedConservationGraphSync> ReadConservationAreas(IRecord record)
    {
        if (!record.ContainsKey("conservationAreas"))
        {
            return [];
        }

        var value = record["conservationAreas"];
        if (value is null)
        {
            return [];
        }

        var items = value.As<List<object?>>();
        var results = new List<GisDerivedConservationGraphSync>();

        foreach (var item in items)
        {
            if (item is not IDictionary<string, object> map)
            {
                continue;
            }

            if (!map.TryGetValue("conservationAreaReferenceId", out var idValue) || idValue is null)
            {
                continue;
            }

            results.Add(new GisDerivedConservationGraphSync(
                Guid.Parse(idValue.ToString()!),
                map["conservationAreaName"]?.ToString() ?? "(unknown conservation area)",
                map.TryGetValue("overlapPercentage", out var overlap) && overlap is not null
                    ? Convert.ToDecimal(overlap)
                    : null,
                map.TryGetValue("source", out var source) && source is not null
                    ? source.ToString()!
                    : GisGraphRelationshipOwnership.SourceName,
                map.TryGetValue("derivedAt", out var derivedAt) && derivedAt is not null
                    ? ParseDerivedAt(derivedAt)
                    : DateTimeOffset.UtcNow));
        }

        return results;
    }

    private static DateTimeOffset ReadDerivedAt(IRecord record, string key)
    {
        if (!record.ContainsKey(key))
        {
            return DateTimeOffset.UtcNow;
        }

        var value = record[key];
        return value is null ? DateTimeOffset.UtcNow : ParseDerivedAt(value);
    }

    private static DateTimeOffset ParseDerivedAt(object value) =>
        value switch
        {
            ZonedDateTime zoned => zoned.ToDateTimeOffset(),
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => DateTimeOffset.Parse(value.ToString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
        };

    private static string? ReadOptionalString(IRecord record, string key)
    {
        if (!record.ContainsKey(key))
        {
            return null;
        }

        var value = record[key];
        return value switch
        {
            null => null,
            string text => text,
            _ => value.As<string>()
        };
    }

    private static decimal? ReadOptionalDecimal(IRecord record, string key)
    {
        if (!record.ContainsKey(key))
        {
            return null;
        }

        var value = record[key];
        return value switch
        {
            null => null,
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue),
            long longValue => longValue,
            int intValue => intValue,
            _ => value.As<decimal?>()
        };
    }
}
