using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

public sealed class ColomboCadastreParcelImportService
{
    public const string CadastreEntry = "Colombo_GIS/Cadastre_Parcels_Landuse_classified___Land_Use_0.geojson";
    public const string DistrictEntry = "Colombo_GIS/Administrative_Boundary__District_1.geojson";
    public const string GnEntry = "Colombo_GIS/Administrative_Boundary__GN_2.geojson";
    public const string SourceName = "Survey Department/iLAS public GIS";
    public const string SourceLayer = "Publishing_Server/LUSE_Cadastre_2026/MapServer/0";

    private const int Srid = 4326;
    private const double EarthRadiusMeters = 6_378_137d;
    private readonly ILogger<ColomboCadastreParcelImportService> _logger;
    private readonly GeoJsonReader _geoJsonReader = new();

    public ColomboCadastreParcelImportService(ILogger<ColomboCadastreParcelImportService> logger) =>
        _logger = logger;

    public async Task<ColomboCadastreImportReport> RunAsync(
        ColomboCadastreImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var zipPath = Path.GetFullPath(options.SourceZip);
        var zipHash = await ComputeFileHashAsync(zipPath, cancellationToken);
        using var archive = ZipFile.OpenRead(zipPath);
        var cadastreEntry = GetRequiredEntry(archive, CadastreEntry);
        var districtEntry = GetRequiredEntry(archive, DistrictEntry);
        var gnEntry = GetRequiredEntry(archive, GnEntry);
        var cadastreHash = await ComputeEntryHashAsync(cadastreEntry, cancellationToken);
        var district = ReadDistrictBoundary(districtEntry, options.District);
        var gnLookup = ReadGnLookup(gnEntry);
        var startedAt = DateTimeOffset.UtcNow;
        var accepted = new List<ColomboParcelImportRow>(Math.Min(options.Limit ?? 2_000, 10_000));
        var skipReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inputCount = 0;
        var duplicateCount = 0;

        await foreach (var feature in ReadFeaturesAsync(cadastreEntry, cancellationToken))
        {
            inputCount++;
            if (TryMap(feature, district, gnLookup, cadastreHash, options.GnCode, out var row, out var skipReason))
            {
                if (!seenIds.Add(row!.SourceFeatureId))
                {
                    duplicateCount++;
                    Count(skipReasons, "duplicate-source-feature-id");
                    continue;
                }

                accepted.Add(row);
                if (options.Limit.HasValue && accepted.Count >= options.Limit.Value)
                {
                    break;
                }
            }
            else
            {
                Count(skipReasons, skipReason!);
            }
        }

        var applied = options.Apply
            ? await ApplyAsync(options.ConnectionString!, accepted, zipHash, cadastreHash, cancellationToken)
            : new ApplyResult(0, 0, null, null, null, null);
        var report = new ColomboCadastreImportReport(
            SourceName,
            SourceLayer,
            zipPath,
            zipHash,
            cadastreHash,
            options.District,
            options.GnCode,
            options.Apply ? "apply" : "dry-run",
            inputCount,
            accepted.Count,
            applied.Inserted,
            applied.Updated,
            skipReasons.Values.Sum(),
            duplicateCount,
            skipReasons,
            applied.DatabaseParcelCount,
            applied.SourceMetadataCount,
            applied.ParcelPolygonCount,
            applied.DemoParcelCount,
            startedAt,
            DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(options.ReportPath))
        {
            var reportPath = Path.GetFullPath(options.ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(
                reportPath,
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
        }

        return report;
    }

    private bool TryMap(
        JObject feature,
        Geometry district,
        IReadOnlyDictionary<string, GnLocation> gnLookup,
        string layerHash,
        string? requiredGnCode,
        out ColomboParcelImportRow? row,
        out string? skipReason)
    {
        row = null;
        skipReason = null;
        var properties = feature["properties"] as JObject;
        if (properties is null)
        {
            skipReason = "missing-properties";
            return false;
        }

        var parcelId = ReadString(properties, "pcl_pg_id");
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            skipReason = "missing-pcl_pg_id";
            return false;
        }

        var gnIndex = ReadString(properties, "adm_gn_ix");
        if (!string.IsNullOrWhiteSpace(requiredGnCode)
            && !string.Equals(gnIndex, requiredGnCode, StringComparison.OrdinalIgnoreCase))
        {
            skipReason = "gn-filter";
            return false;
        }

        Geometry geometry;
        try
        {
            geometry = _geoJsonReader.Read<Geometry>(feature["geometry"]?.ToString(Formatting.None) ?? "null");
        }
        catch (Exception)
        {
            skipReason = "invalid-geometry-json";
            return false;
        }

        var boundary = geometry switch
        {
            Polygon polygon => polygon.Factory.CreateMultiPolygon([polygon]),
            MultiPolygon multiPolygon => multiPolygon,
            _ => null
        };
        if (boundary is null || boundary.IsEmpty || !boundary.IsValid)
        {
            skipReason = "invalid-or-non-polygon-geometry";
            return false;
        }

        boundary.SRID = Srid;
        if (!district.Intersects(boundary))
        {
            skipReason = "outside-colombo";
            return false;
        }

        var centroid = boundary.Centroid;
        centroid.SRID = Srid;
        var areaHectares = CalculateGeodesicAreaSquareMeters(boundary) / 10_000d;
        if (!double.IsFinite(areaHectares) || areaHectares <= 0)
        {
            skipReason = "invalid-area";
            return false;
        }

        gnLookup.TryGetValue(gnIndex ?? string.Empty, out var gn);
        var objectId = ReadString(properties, "OBJECTID") ?? ReadString(properties, "objectid");
        var sourceFingerprint = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{layerHash}:{objectId ?? parcelId}"))).ToLowerInvariant();
        var landUseCode = ReadFieldAsString(properties, "pcl_if_luc");
        var landUseText = ReadString(properties, "pcl_if_lu");
        var landUseEnglish = ColomboCadastreSourceDomains.TryDecodeLandUseEnglishLabel(landUseCode);
        var claimantCategoryCode = ReadFieldAsString(properties, "pcl_if_clc");
        var claimantCategoryEnglish = ColomboCadastreSourceDomains.TryDecodeClaimantCategoryEnglishLabel(claimantCategoryCode);
        row = new ColomboParcelImportRow(
            parcelId,
            ReadString(properties, "pcl_pg_id2"),
            ReadString(properties, "sht_pg_id2"),
            ReadString(properties, "pcl_pg_st"),
            landUseCode,
            landUseEnglish,
            landUseText,
            ReadFieldAsString(properties, "pcl_if_cs"),
            gnIndex,
            claimantCategoryCode,
            claimantCategoryEnglish,
            objectId,
            ReadString(properties, "globalid"),
            sourceFingerprint,
            Convert.ToDecimal(areaHectares, CultureInfo.InvariantCulture),
            "Western",
            "Colombo",
            gn?.DivisionalSecretariat ?? "Unavailable",
            gn?.GramaNiladhariDivision,
            centroid,
            boundary);
        return true;
    }

    private async Task<ApplyResult> ApplyAsync(
        string connectionString,
        IReadOnlyList<ColomboParcelImportRow> rows,
        string zipHash,
        string layerHash,
        CancellationToken cancellationToken)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseNetTopologySuite();
        await using var dataSource = dataSourceBuilder.Build();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureSourceTableAsync(connection, transaction, cancellationToken);
        var lookupIds = await ReadLookupIdsAsync(connection, transaction, cancellationToken);
        var existing = await CountExistingAsync(connection, transaction, rows.Select(x => x.SourceFeatureId), cancellationToken);

        foreach (var chunk in rows.Chunk(500))
        {
            foreach (var item in chunk)
            {
                await UpsertParcelAsync(connection, transaction, item, lookupIds, zipHash, layerHash, cancellationToken);
            }
        }

        const string verificationSql = """
            SELECT
              (SELECT count(*) FROM land_intelligence.land_parcels),
              (SELECT count(*) FROM land_intelligence.real_parcel_sources),
              (SELECT count(*) FROM land_intelligence.land_parcels WHERE "Boundary" IS NOT NULL),
              (SELECT count(*) FROM land_intelligence.land_parcels WHERE "CadastralNumber" LIKE 'DEMO-%');
            """;
        await using var verification = new NpgsqlCommand(verificationSql, connection, transaction);
        await using var verificationReader = await verification.ExecuteReaderAsync(cancellationToken);
        await verificationReader.ReadAsync(cancellationToken);
        var databaseCount = checked((int)verificationReader.GetInt64(0));
        var sourceCount = checked((int)verificationReader.GetInt64(1));
        var polygonCount = checked((int)verificationReader.GetInt64(2));
        var demoCount = checked((int)verificationReader.GetInt64(3));
        await verificationReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new ApplyResult(rows.Count - existing, existing, databaseCount, sourceCount, polygonCount, demoCount);
    }

    private static async Task EnsureSourceTableAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS land_intelligence.real_parcel_sources (
                "LandParcelId" uuid PRIMARY KEY REFERENCES land_intelligence.land_parcels("Id") ON DELETE CASCADE,
                "SourceName" varchar(200) NOT NULL,
                "SourceLayer" varchar(300) NOT NULL,
                "SourceFeatureId" varchar(100) NOT NULL UNIQUE,
                "SourceFingerprint" char(64) NOT NULL,
                "ImportedAtUtc" timestamptz NOT NULL,
                "SourceZipSha256" char(64) NOT NULL,
                "SourceLayerSha256" char(64) NOT NULL,
                "ParcelId2" varchar(150), "SheetId2" varchar(150), "ParcelStatus" varchar(100),
                "LandUseCode" varchar(100), "LandUseDescription" varchar(300), "CoordinateStatus" varchar(100),
                "GnIndex" varchar(100), "ClaimantCategory" varchar(100), "SourceObjectId" varchar(100), "SourceGlobalId" varchar(100),
                "LandUseEnglishLabel" varchar(200), "ClaimantCategoryCode" varchar(100), "ClaimantCategoryEnglishLabel" varchar(200)
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);

        const string alterSql = """
            ALTER TABLE land_intelligence.real_parcel_sources ADD COLUMN IF NOT EXISTS "LandUseEnglishLabel" varchar(200);
            ALTER TABLE land_intelligence.real_parcel_sources ADD COLUMN IF NOT EXISTS "ClaimantCategoryCode" varchar(100);
            ALTER TABLE land_intelligence.real_parcel_sources ADD COLUMN IF NOT EXISTS "ClaimantCategoryEnglishLabel" varchar(200);
            """;
        await using var alter = new NpgsqlCommand(alterSql, connection, transaction);
        await alter.ExecuteNonQueryAsync(ct);
    }

    private static async Task<LookupIds> ReadLookupIdsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        var categories = new Dictionary<int, Guid>();
        var uses = new Dictionary<int, Guid>();
        await using (var command = new NpgsqlCommand("SELECT \"Id\", \"Type\" FROM land_intelligence.land_categories", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) categories[reader.GetInt32(1)] = reader.GetGuid(0);
        await using (var command = new NpgsqlCommand("SELECT \"Id\", \"Type\" FROM land_intelligence.land_uses", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) uses[reader.GetInt32(1)] = reader.GetGuid(0);
        if (!categories.TryGetValue((int)LandCategoryType.Other, out var otherCategory))
            throw new InvalidOperationException("The Other land category lookup is missing. Run database migrations/seed first.");
        return new LookupIds(otherCategory, uses);
    }

    private static async Task<int> CountExistingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IEnumerable<string> ids, CancellationToken ct)
    {
        var array = ids.ToArray();
        if (array.Length == 0) return 0;
        await using var command = new NpgsqlCommand("SELECT count(*) FROM land_intelligence.land_parcels WHERE \"CadastralNumber\" = ANY(@ids)", connection, transaction);
        command.Parameters.AddWithValue("ids", array);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    private static async Task UpsertParcelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ColomboParcelImportRow item, LookupIds lookup, string zipHash, string layerHash, CancellationToken ct)
    {
        var id = CreateDeterministicGuid(item.SourceFeatureId);
        const string parcelSql = """
            INSERT INTO land_intelligence.land_parcels
            ("Id","CadastralNumber","SurveyPlanReference","LandCategoryId","CurrentLandUseId","AreaValue","AreaUnit","Province","District","DivisionalSecretariat","GramaNiladhariDivision","Centroid","Boundary","SpatialReferenceSystemId")
            VALUES (@id,@number,@sheet,@category,NULL,@area,@unit,@province,@district,@ds,@gn,@centroid,@boundary,4326)
            ON CONFLICT ("CadastralNumber") DO UPDATE SET
            "SurveyPlanReference"=EXCLUDED."SurveyPlanReference", "CurrentLandUseId"=NULL,
            "AreaValue"=EXCLUDED."AreaValue", "AreaUnit"=EXCLUDED."AreaUnit", "Province"=EXCLUDED."Province",
            "District"=EXCLUDED."District", "DivisionalSecretariat"=EXCLUDED."DivisionalSecretariat",
            "GramaNiladhariDivision"=EXCLUDED."GramaNiladhariDivision", "Centroid"=EXCLUDED."Centroid",
            "Boundary"=EXCLUDED."Boundary", "SpatialReferenceSystemId"=4326 RETURNING "Id";
            """;
        await using var parcel = new NpgsqlCommand(parcelSql, connection, transaction);
        parcel.Parameters.AddWithValue("id", id); parcel.Parameters.AddWithValue("number", item.SourceFeatureId);
        parcel.Parameters.AddWithValue("sheet", (object?)item.SheetId2 ?? DBNull.Value); parcel.Parameters.AddWithValue("category", lookup.OtherCategoryId);
        parcel.Parameters.AddWithValue("area", item.AreaHectares);
        parcel.Parameters.AddWithValue("unit", (int)AreaUnit.Hectares); parcel.Parameters.AddWithValue("province", item.Province);
        parcel.Parameters.AddWithValue("district", item.District); parcel.Parameters.AddWithValue("ds", item.DivisionalSecretariat);
        parcel.Parameters.AddWithValue("gn", (object?)item.GramaNiladhariDivision ?? DBNull.Value);
        parcel.Parameters.AddWithValue("centroid", NpgsqlDbType.Geometry, item.Centroid);
        parcel.Parameters.AddWithValue("boundary", NpgsqlDbType.Geometry, item.Boundary);
        var persistedId = (Guid)(await parcel.ExecuteScalarAsync(ct))!;

        const string sourceSql = """
            INSERT INTO land_intelligence.real_parcel_sources
            ("LandParcelId","SourceName","SourceLayer","SourceFeatureId","SourceFingerprint","ImportedAtUtc","SourceZipSha256","SourceLayerSha256","ParcelId2","SheetId2","ParcelStatus","LandUseCode","LandUseDescription","CoordinateStatus","GnIndex","ClaimantCategory","SourceObjectId","SourceGlobalId","LandUseEnglishLabel","ClaimantCategoryCode","ClaimantCategoryEnglishLabel")
            VALUES (@parcelId,@sourceName,@sourceLayer,@featureId,@fingerprint,now(),@zipHash,@layerHash,@parcelId2,@sheetId2,@status,@landUseCode,@landUseText,@coordinateStatus,@gnIndex,@claimantCategoryLegacy,@objectId,@globalId,@landUseEnglish,@claimantCategoryCode,@claimantCategoryEnglish)
            ON CONFLICT ("LandParcelId") DO UPDATE SET "SourceFingerprint"=EXCLUDED."SourceFingerprint","ImportedAtUtc"=now(),"SourceZipSha256"=EXCLUDED."SourceZipSha256","SourceLayerSha256"=EXCLUDED."SourceLayerSha256","ParcelId2"=EXCLUDED."ParcelId2","SheetId2"=EXCLUDED."SheetId2","ParcelStatus"=EXCLUDED."ParcelStatus","LandUseCode"=EXCLUDED."LandUseCode","LandUseDescription"=EXCLUDED."LandUseDescription","CoordinateStatus"=EXCLUDED."CoordinateStatus","GnIndex"=EXCLUDED."GnIndex","ClaimantCategory"=EXCLUDED."ClaimantCategory","SourceObjectId"=EXCLUDED."SourceObjectId","SourceGlobalId"=EXCLUDED."SourceGlobalId","LandUseEnglishLabel"=EXCLUDED."LandUseEnglishLabel","ClaimantCategoryCode"=EXCLUDED."ClaimantCategoryCode","ClaimantCategoryEnglishLabel"=EXCLUDED."ClaimantCategoryEnglishLabel";
            """;
        await using var source = new NpgsqlCommand(sourceSql, connection, transaction);
        void Add(string name, object? value) => source.Parameters.AddWithValue(name, value ?? DBNull.Value);
        Add("parcelId", persistedId); Add("sourceName", SourceName); Add("sourceLayer", SourceLayer); Add("featureId", item.SourceFeatureId);
        Add("fingerprint", item.SourceFingerprint); Add("zipHash", zipHash); Add("layerHash", layerHash); Add("parcelId2", item.ParcelId2);
        Add("sheetId2", item.SheetId2); Add("status", item.ParcelStatus); Add("landUseCode", item.LandUseCode); Add("landUseText", item.LandUseSourceText);
        Add("coordinateStatus", item.CoordinateStatus); Add("gnIndex", item.GnIndex);
        Add("claimantCategoryLegacy", item.ClaimantCategoryCode);
        Add("objectId", item.SourceObjectId); Add("globalId", item.SourceGlobalId);
        Add("landUseEnglish", item.LandUseEnglishLabel); Add("claimantCategoryCode", item.ClaimantCategoryCode);
        Add("claimantCategoryEnglish", item.ClaimantCategoryEnglishLabel);
        await source.ExecuteNonQueryAsync(ct);
    }

    private async IAsyncEnumerable<JObject> ReadFeaturesAsync(ZipArchiveEntry entry, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = entry.Open();
        using var text = new StreamReader(stream);
        using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
        while (await reader.ReadAsync(ct))
        {
            if (reader.TokenType != JsonToken.PropertyName || !string.Equals((string?)reader.Value, "features", StringComparison.Ordinal)) continue;
            if (!await reader.ReadAsync(ct) || reader.TokenType != JsonToken.StartArray) throw new InvalidDataException("GeoJSON features array is missing.");
            while (await reader.ReadAsync(ct) && reader.TokenType != JsonToken.EndArray)
            {
                if (reader.TokenType == JsonToken.StartObject) yield return await JObject.LoadAsync(reader, ct);
            }
            yield break;
        }
        throw new InvalidDataException("GeoJSON features array is missing.");
    }

    private Geometry ReadDistrictBoundary(ZipArchiveEntry entry, string districtName)
    {
        using var stream = entry.Open(); using var text = new StreamReader(stream); using var reader = new JsonTextReader(text);
        var root = JObject.Load(reader);
        var feature = root["features"]?.Children<JObject>().SingleOrDefault(item =>
            string.Equals(ReadString((JObject)item["properties"]!, "DISTRICT_N"), districtName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"District '{districtName}' was not found in {DistrictEntry}.");
        var geometry = _geoJsonReader.Read<Geometry>(feature["geometry"]!.ToString(Formatting.None)); geometry.SRID = Srid; return geometry;
    }

    private static IReadOnlyDictionary<string, GnLocation> ReadGnLookup(ZipArchiveEntry entry)
    {
        using var stream = entry.Open(); using var text = new StreamReader(stream); using var reader = new JsonTextReader(text);
        var result = new Dictionary<string, GnLocation>(StringComparer.OrdinalIgnoreCase);
        var root = JObject.Load(reader);
        foreach (var feature in root["features"]?.Children<JObject>() ?? [])
        {
            var p = (JObject?)feature["properties"]; if (p is null) continue;
            var key = ReadString(p, "CM_Index") ?? ReadString(p, "OBJECTID"); if (string.IsNullOrWhiteSpace(key)) continue;
            result[key] = new GnLocation(ReadString(p, "DSD_N") ?? "Unavailable", ReadString(p, "GND_N") ?? ReadString(p, "GND_NAME_Cen"));
        }
        return result;
    }

    private static double CalculateGeodesicAreaSquareMeters(MultiPolygon geometry)
    {
        double total = 0;
        foreach (Polygon polygon in geometry.Geometries)
        {
            total += Math.Abs(RingArea(polygon.ExteriorRing.Coordinates));
            for (var i = 0; i < polygon.NumInteriorRings; i++) total -= Math.Abs(RingArea(polygon.GetInteriorRingN(i).Coordinates));
        }
        return Math.Abs(total);
    }

    private static double RingArea(Coordinate[] ring)
    {
        if (ring.Length < 4) return 0;
        double sum = 0;
        for (var i = 0; i < ring.Length - 1; i++)
        {
            var lon1 = DegreesToRadians(ring[i].X); var lon2 = DegreesToRadians(ring[i + 1].X);
            var lat1 = DegreesToRadians(ring[i].Y); var lat2 = DegreesToRadians(ring[i + 1].Y);
            var deltaLon = lon2 - lon1;
            if (deltaLon > Math.PI) deltaLon -= 2 * Math.PI; else if (deltaLon < -Math.PI) deltaLon += 2 * Math.PI;
            sum += deltaLon * (2 + Math.Sin(lat1) + Math.Sin(lat2));
        }
        return sum * EarthRadiusMeters * EarthRadiusMeters / 2d;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static string? ReadString(JObject properties, string name) => properties.GetValue(name, StringComparison.OrdinalIgnoreCase)?.ToString()?.Trim() is { Length: > 0 } value ? value : null;

    private static string? ReadFieldAsString(JObject properties, string name)
    {
        var token = properties.GetValue(name, StringComparison.OrdinalIgnoreCase);
        if (token is null || token.Type == JTokenType.Null)
        {
            return null;
        }

        return token.ToString().Trim() is { Length: > 0 } value ? value : null;
    }
    private static void Count(Dictionary<string, int> values, string key) => values[key] = values.GetValueOrDefault(key) + 1;
    private static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string name) => archive.GetEntry(name) ?? throw new InvalidDataException($"Required ZIP entry is missing: {name}");
    private static Guid CreateDeterministicGuid(string value) => new(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{SourceLayer}:{value}"))[..16]);
    private static void ValidateOptions(ColomboCadastreImportOptions options)
    {
        if (!File.Exists(options.SourceZip)) throw new FileNotFoundException("Source ZIP was not found.", options.SourceZip);
        if (!string.Equals(options.District, "Colombo", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("This importer accepts only --district Colombo.");
        if (options.Limit is <= 0) throw new ArgumentOutOfRangeException(nameof(options.Limit));
        if (options.Apply && string.IsNullOrWhiteSpace(options.ConnectionString)) throw new ArgumentException("--apply requires an explicit connection string or environment variable.");
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken ct) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant(); }
    private static async Task<string> ComputeEntryHashAsync(ZipArchiveEntry entry, CancellationToken ct) { await using var stream = entry.Open(); return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant(); }
    private sealed record GnLocation(string DivisionalSecretariat, string? GramaNiladhariDivision);
    private sealed record LookupIds(Guid OtherCategoryId, IReadOnlyDictionary<int, Guid> LandUses);
    private sealed record ApplyResult(int Inserted, int Updated, int? DatabaseParcelCount, int? SourceMetadataCount, int? ParcelPolygonCount, int? DemoParcelCount);
}

public sealed record ColomboCadastreImportOptions(string SourceZip, string District = "Colombo", int? Limit = null, string? GnCode = null, bool Apply = false, string? ConnectionString = null, string? ReportPath = null);
public sealed record ColomboCadastreImportReport(string SourceName, string SourceLayer, string SourceZip, string SourceZipSha256, string SourceLayerSha256, string District, string? GnCode, string Mode, int InputFeaturesRead, int ValidFeatures, int Inserted, int Updated, int Skipped, int Duplicates, IReadOnlyDictionary<string, int> SkipReasons, int? DatabaseParcelCount, int? SourceMetadataCount, int? ParcelPolygonCount, int? DemoParcelCount, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc);
public sealed record ColomboParcelImportRow(
    string SourceFeatureId,
    string? ParcelId2,
    string? SheetId2,
    string? ParcelStatus,
    string? LandUseCode,
    string? LandUseEnglishLabel,
    string? LandUseSourceText,
    string? CoordinateStatus,
    string? GnIndex,
    string? ClaimantCategoryCode,
    string? ClaimantCategoryEnglishLabel,
    string? SourceObjectId,
    string? SourceGlobalId,
    string SourceFingerprint,
    decimal AreaHectares,
    string Province,
    string District,
    string DivisionalSecretariat,
    string? GramaNiladhariDivision,
    Point Centroid,
    MultiPolygon Boundary);
