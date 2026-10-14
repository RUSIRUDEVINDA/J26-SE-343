using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

const string expectedDatabase = "land_intel_colombo_state_research";
const string sourceLayer = "Cadastre Parcels(Landuse classified) / Land_Use";
const string sourceZipSha256 = "771ddd15009e791cf55533c5f29b6161a0b3b48095a329008b2d120bbe6612e0";
const string supplementDatasetKey = "colombo-state-research-supplement-100";
const string datasetRelative = "data/gis/experiments/colombo/state_research_supplement_100";
const string originalRelative = "data/gis/experiments/colombo/state_research_300/colombo_state_parcels_300_real.csv";

try
{
    var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
    if (args.Any(arg => arg is not ("--apply" or "--dry-run")))
        throw new ArgumentException("Use --dry-run (default) or --apply.");
    if (apply && args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase))
        throw new ArgumentException("Choose one mode.");

    var root = FindProjectRoot();
    var data = Path.Combine(root, datasetRelative);
    var csvPath = Path.Combine(data, "colombo_state_supplement_100.csv");
    var geoPath = Path.Combine(data, "colombo_state_supplement_100.geojson");
    var reportPath = Path.Combine(data, "supplement_100_validation_report.json");
    var originalPath = Path.Combine(root, originalRelative);
    using var report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
    var expectedFiles = report.RootElement.GetProperty("files");
    RequireHash(csvPath, expectedFiles.GetProperty(Path.GetFileName(csvPath)).GetString()!);
    RequireHash(geoPath, expectedFiles.GetProperty(Path.GetFileName(geoPath)).GetString()!);
    RequireHash(originalPath, report.RootElement.GetProperty("existing_300_csv_sha256").GetString()!);

    var originalIds = CsvIds(originalPath);
    var supplementIds = CsvIds(csvPath);
    if (originalIds.Count != 300 || supplementIds.Count != 100 || originalIds.Overlaps(supplementIds))
        throw new InvalidDataException("Expected 300 original IDs and 100 distinct new IDs.");

    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(geoPath));
    var features = document.RootElement.GetProperty("features").EnumerateArray().ToArray();
    if (features.Length != 100) throw new InvalidDataException("Expected exactly 100 GeoJSON features.");
    var geoReader = new GeoJsonReader();
    var rows = new List<ParcelRow>(100);
    foreach (var feature in features)
    {
        var p = feature.GetProperty("properties");
        var id = p.GetProperty("parcel_source_id").GetString()!;
        var recordId = p.GetProperty("record_id").GetString()!;
        if (!supplementIds.Contains(id) || recordId != "COL-SUPP-" + id
            || p.GetProperty("district").GetString() != "Colombo"
            || p.GetProperty("source_claimant_category_code").GetInt32() != 2
            || p.GetProperty("source_claimant_category_label").GetString() != "State"
            || p.GetProperty("lease_availability").GetString() != "Not verified"
            || p.GetProperty("project_lease_ready_designation").GetString() != ColomboStateResearchProjectLeaseReady.DesignationLabel)
            throw new InvalidDataException($"Unsafe supplement record: {id}");
        var group = p.GetProperty("supplement_group").GetString()!;
        if (group is not ("Wetland" or "Scrub / low jungle" or "Coconut" or "Other State parcel"))
            throw new InvalidDataException($"Unexpected category for {id}: {group}");
        var basis = p.GetProperty("classification_basis").GetString()!;
        var overlap = p.GetProperty("gis_overlap_percentages_json").GetString()!;
        if (basis == "GIS-derived LUPPD coverage (at least 80%)")
        {
            using var percentages = JsonDocument.Parse(overlap);
            var sourceGroup = group == "Scrub / low jungle" ? "Scrub" : group;
            if (!percentages.RootElement.TryGetProperty(sourceGroup, out var percentage)
                || percentage.GetDouble() < 80)
                throw new InvalidDataException($"GIS category for {id} lacks 80% coverage.");
        }
        else if (basis != "Original cadastral land-use label")
            throw new InvalidDataException($"Unknown classification basis for {id}.");
        var geometryJson = feature.GetProperty("geometry").GetRawText();
        var geometry = geoReader.Read<Geometry>(geometryJson);
        if (geometry is not (Polygon or MultiPolygon) || !geometry.IsValid || geometry.IsEmpty)
            throw new InvalidDataException($"Invalid original boundary for {id}.");
        var lat = p.GetProperty("centroid_latitude").GetDouble();
        var lon = p.GetProperty("centroid_longitude").GetDouble();
        var markerLat = p.GetProperty("map_marker_latitude").GetDouble();
        var markerLon = p.GetProperty("map_marker_longitude").GetDouble();
        var marker = geometry.Factory.CreatePoint(new Coordinate(markerLon, markerLat));
        if (!geometry.Covers(marker) || !double.IsFinite(lat) || !double.IsFinite(lon))
            throw new InvalidDataException($"Invalid centroid/map marker for {id}.");
        rows.Add(new ParcelRow(id, recordId, p.GetProperty("source_object_id").ToString(),
            p.GetProperty("source_parcel_id2").GetString(), p.GetProperty("source_sheet_id2").GetString(),
            p.GetProperty("source_gn_index").ToString(), p.GetProperty("source_land_use_code").GetString()!,
            p.GetProperty("source_land_use_label").GetString()!, group, basis, overlap,
            p.GetProperty("gis_evidence_json").GetString()!,
            p.GetProperty("area_hectares").GetDecimal(), lat, lon, markerLat, markerLon,
            p.GetProperty("live_verified_at_utc").GetDateTimeOffset(), geometryJson));
    }
    if (rows.Select(r => r.Id).Distinct().Count() != 100)
        throw new InvalidDataException("Duplicate GeoJSON parcel ID.");

    var counts = rows.GroupBy(r => r.Group).ToDictionary(g => g.Key, g => g.Count());
    if (counts.GetValueOrDefault("Wetland") != 38 || counts.GetValueOrDefault("Scrub / low jungle") != 26
        || counts.GetValueOrDefault("Coconut") != 20 || counts.GetValueOrDefault("Other State parcel") != 16)
        throw new InvalidDataException("Supplement category counts changed.");

    if (!apply)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { mode = "dry-run", valid = rows.Count, categories = counts,
            originalDatasetCount = originalIds.Count, originalDatasetPreserved = true,
            supplementProjectDesignationCount = rows.Count }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    var connectionString = Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_STATE_RESEARCH_CONNECTION");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("LAND_INTELLIGENCE_STATE_RESEARCH_CONNECTION is not set.");
    ColomboStateResearch300ImportService.AssertResearchDatabase(connectionString);
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();

    var presentOriginal = await CountIds(connection, transaction, originalIds);
    if (presentOriginal != 300)
        throw new InvalidOperationException($"Original dataset is incomplete in {expectedDatabase}: found {presentOriginal}/300. Import it first.");
    var originalDesignations = await CountDesignations(connection, transaction, ColomboStateResearchProjectLeaseReady.DatasetKey);
    if (originalDesignations != 300)
        throw new InvalidOperationException("Original 300 project designations are incomplete; refusing supplement apply.");
    await using (var collisions = new NpgsqlCommand("""
        SELECT count(*) FROM land_intelligence.land_parcels p
        LEFT JOIN land_intelligence.real_parcel_sources s ON s."LandParcelId" = p."Id"
        WHERE p."CadastralNumber" = ANY(@ids)
          AND (s."ResearchRecordId" IS DISTINCT FROM 'COL-SUPP-' || p."CadastralNumber");
        """, connection, transaction))
    {
        collisions.Parameters.AddWithValue("ids", rows.Select(r => r.Id).ToArray());
        if (Convert.ToInt64(await collisions.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 0)
            throw new InvalidOperationException("A supplement source ID already belongs to another parcel import. Refusing to overwrite it.");
    }
    Guid categoryId;
    await using (var category = new NpgsqlCommand("SELECT \"Id\" FROM land_intelligence.land_categories WHERE \"Type\"=99", connection, transaction))
        categoryId = (Guid?)await category.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The Other category lookup is missing; run existing database migrations/seed first.");

    await using (var table = new NpgsqlCommand("""
        CREATE TABLE IF NOT EXISTS land_intelligence.research_supplement_land_cover (
            "LandParcelId" uuid PRIMARY KEY REFERENCES land_intelligence.land_parcels("Id") ON DELETE CASCADE,
            "ResearchRecordId" varchar(120) NOT NULL UNIQUE,
            "SupplementGroup" varchar(100) NOT NULL,
            "ClassificationBasis" varchar(150) NOT NULL,
            "OriginalLandUseCode" varchar(100) NOT NULL,
            "OriginalLandUseLabel" varchar(200) NOT NULL,
            "GisOverlapPercentages" jsonb NOT NULL,
            "GisEvidence" jsonb NOT NULL,
            "SourceLayerUrl" varchar(500) NOT NULL,
            "GisLayerUrl" varchar(500) NOT NULL,
            "LiveVerifiedAtUtc" timestamptz NOT NULL,
            "ImportedAtUtc" timestamptz NOT NULL DEFAULT now()
        );
        """, connection, transaction))
        await table.ExecuteNonQueryAsync();

    var inserted = 0;
    var geoHash = Hash(geoPath);
    foreach (var row in rows)
    {
        var existing = await CountIds(connection, transaction, [row.Id]);
        if (existing == 0) inserted++;
        var parcelId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(sourceLayer + ":" + row.Id))[..16]);
        await using (var parcel = new NpgsqlCommand("""
            INSERT INTO land_intelligence.land_parcels
            ("Id","CadastralNumber","SurveyPlanReference","LandCategoryId","CurrentLandUseId","AreaValue","AreaUnit","Province","District","DivisionalSecretariat","GramaNiladhariDivision","Centroid","Boundary","SpatialReferenceSystemId")
            VALUES (@id,@number,@sheet,@category,NULL,@area,2,'Western','Colombo','Unavailable',NULL,
              ST_SetSRID(ST_MakePoint(@lon,@lat),4326),ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(@geometry),4326)),4326)
            ON CONFLICT ("CadastralNumber") DO UPDATE SET
              "SurveyPlanReference"=EXCLUDED."SurveyPlanReference", "AreaValue"=EXCLUDED."AreaValue",
              "Centroid"=EXCLUDED."Centroid", "Boundary"=EXCLUDED."Boundary"
            RETURNING "Id";
            """, connection, transaction))
        {
            parcel.Parameters.AddWithValue("id", parcelId);
            parcel.Parameters.AddWithValue("number", row.Id);
            parcel.Parameters.AddWithValue("sheet", (object?)row.SheetId ?? DBNull.Value);
            parcel.Parameters.AddWithValue("category", categoryId);
            parcel.Parameters.AddWithValue("area", row.Area);
            parcel.Parameters.AddWithValue("lon", row.Lon);
            parcel.Parameters.AddWithValue("lat", row.Lat);
            parcel.Parameters.AddWithValue("geometry", row.GeometryJson);
            parcelId = (Guid)(await parcel.ExecuteScalarAsync())!;
        }
        await using (var source = new NpgsqlCommand("""
            INSERT INTO land_intelligence.real_parcel_sources
            ("LandParcelId","SourceName","SourceLayer","SourceFeatureId","SourceFingerprint","ImportedAtUtc","SourceZipSha256","SourceLayerSha256","ParcelId2","SheetId2","ParcelStatus","LandUseCode","LandUseDescription","CoordinateStatus","GnIndex","ClaimantCategory","SourceObjectId","SourceGlobalId","LandUseEnglishLabel","ClaimantCategoryCode","ClaimantCategoryEnglishLabel","ResearchRecordId","MapMarkerLatitude","MapMarkerLongitude")
            VALUES (@pid,'Survey Department public ArcGIS export',@layer,@feature,@fingerprint,now(),@ziphash,@geohash,@parcel2,@sheet,NULL,@landcode,@landlabel,'ValidOriginalBoundary',@gn,'2',@object,NULL,@landlabel,'2','State',@record,@markerlat,@markerlon)
            ON CONFLICT ("LandParcelId") DO UPDATE SET
              "SourceFingerprint"=EXCLUDED."SourceFingerprint", "ImportedAtUtc"=now(),
              "SourceLayerSha256"=EXCLUDED."SourceLayerSha256", "LandUseCode"=EXCLUDED."LandUseCode",
              "LandUseDescription"=EXCLUDED."LandUseDescription", "LandUseEnglishLabel"=EXCLUDED."LandUseEnglishLabel",
              "MapMarkerLatitude"=EXCLUDED."MapMarkerLatitude", "MapMarkerLongitude"=EXCLUDED."MapMarkerLongitude";
            """, connection, transaction))
        {
            source.Parameters.AddWithValue("pid", parcelId);
            source.Parameters.AddWithValue("layer", sourceLayer);
            source.Parameters.AddWithValue("feature", row.Id);
            source.Parameters.AddWithValue("fingerprint", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(geoHash + ":" + row.RecordId))).ToLowerInvariant());
            source.Parameters.AddWithValue("ziphash", sourceZipSha256);
            source.Parameters.AddWithValue("geohash", geoHash);
            source.Parameters.AddWithValue("parcel2", (object?)row.ParcelId2 ?? DBNull.Value);
            source.Parameters.AddWithValue("sheet", (object?)row.SheetId ?? DBNull.Value);
            source.Parameters.AddWithValue("landcode", row.LandUseCode);
            source.Parameters.AddWithValue("landlabel", row.LandUseLabel);
            source.Parameters.AddWithValue("gn", (object?)row.GnIndex ?? DBNull.Value);
            source.Parameters.AddWithValue("object", row.ObjectId);
            source.Parameters.AddWithValue("record", row.RecordId);
            source.Parameters.AddWithValue("markerlat", row.MarkerLat);
            source.Parameters.AddWithValue("markerlon", row.MarkerLon);
            await source.ExecuteNonQueryAsync();
        }
        await using (var cover = new NpgsqlCommand("""
            INSERT INTO land_intelligence.research_supplement_land_cover
            ("LandParcelId","ResearchRecordId","SupplementGroup","ClassificationBasis","OriginalLandUseCode","OriginalLandUseLabel","GisOverlapPercentages","GisEvidence","SourceLayerUrl","GisLayerUrl","LiveVerifiedAtUtc")
            VALUES (@pid,@record,@group,@basis,@code,@label,@overlap::jsonb,@evidence::jsonb,@sourceurl,@gisurl,@verified)
            ON CONFLICT ("LandParcelId") DO UPDATE SET
              "SupplementGroup"=EXCLUDED."SupplementGroup", "ClassificationBasis"=EXCLUDED."ClassificationBasis",
              "OriginalLandUseCode"=EXCLUDED."OriginalLandUseCode", "OriginalLandUseLabel"=EXCLUDED."OriginalLandUseLabel",
              "GisOverlapPercentages"=EXCLUDED."GisOverlapPercentages", "GisEvidence"=EXCLUDED."GisEvidence",
              "LiveVerifiedAtUtc"=EXCLUDED."LiveVerifiedAtUtc", "ImportedAtUtc"=now();
            """, connection, transaction))
        {
            cover.Parameters.AddWithValue("pid", parcelId);
            cover.Parameters.AddWithValue("record", row.RecordId);
            cover.Parameters.AddWithValue("group", row.Group);
            cover.Parameters.AddWithValue("basis", row.Basis);
            cover.Parameters.AddWithValue("code", row.LandUseCode);
            cover.Parameters.AddWithValue("label", row.LandUseLabel);
            cover.Parameters.AddWithValue("overlap", row.OverlapJson);
            cover.Parameters.AddWithValue("evidence", row.EvidenceJson);
            cover.Parameters.AddWithValue("sourceurl", "https://lis.survey.gov.lk/server/rest/services/Publishing_Server/LUSE_Cadastre_2026/MapServer/0");
            cover.Parameters.AddWithValue("gisurl", "https://lis.survey.gov.lk/server/rest/services/Publishing_Server/Land_Use_LUPPD/MapServer/0");
            cover.Parameters.AddWithValue("verified", row.VerifiedAt);
            await cover.ExecuteNonQueryAsync();
        }
        await using (var designation = new NpgsqlCommand("""
            INSERT INTO land_intelligence.colombo_state_research_project_lease_ready
            ("ResearchRecordId","LandParcelId","DatasetKey","DesignationLabel","DesignationNotice","ImportedAtUtc")
            VALUES (@record,@pid,@dataset,@label,@notice,now())
            ON CONFLICT ("ResearchRecordId") DO UPDATE SET
              "LandParcelId"=EXCLUDED."LandParcelId", "DatasetKey"=EXCLUDED."DatasetKey",
              "DesignationLabel"=EXCLUDED."DesignationLabel", "DesignationNotice"=EXCLUDED."DesignationNotice",
              "ImportedAtUtc"=now();
            """, connection, transaction))
        {
            designation.Parameters.AddWithValue("record", row.RecordId);
            designation.Parameters.AddWithValue("pid", parcelId);
            designation.Parameters.AddWithValue("dataset", supplementDatasetKey);
            designation.Parameters.AddWithValue("label", ColomboStateResearchProjectLeaseReady.DesignationLabel);
            designation.Parameters.AddWithValue("notice", ColomboStateResearchProjectLeaseReady.DesignationNotice);
            await designation.ExecuteNonQueryAsync();
        }
    }
    var finalOriginal = await CountIds(connection, transaction, originalIds);
    var finalSupplement = await CountIds(connection, transaction, supplementIds);
    var finalSupplementDesignations = await CountDesignations(connection, transaction, supplementDatasetKey);
    var finalOriginalDesignations = await CountDesignations(connection, transaction, ColomboStateResearchProjectLeaseReady.DatasetKey);
    if (finalOriginal != 300 || finalSupplement != 100 || finalOriginalDesignations != 300 || finalSupplementDesignations != 100)
        throw new InvalidOperationException("Post-import parcel counts failed; rolling back.");
    await transaction.CommitAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { mode = "apply", inserted, updated = 100 - inserted,
        originalPreserved = finalOriginal, supplementCount = finalSupplement, categories = counts,
        originalProjectDesignations = finalOriginalDesignations,
        supplementProjectDesignations = finalSupplementDesignations }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static string FindProjectRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "src", "Api", "StateLandGovernance.Api.csproj")))
            return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Run from StateLandGovernance or a child directory.");
}

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static void RequireHash(string path, string expected)
{
    if (!string.Equals(Hash(path), expected, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException($"Dataset file changed: {Path.GetFileName(path)}");
}

static HashSet<string> CsvIds(string path)
{
    var ids = new HashSet<string>(StringComparer.Ordinal);
    using var parser = new TextFieldParser(path, Encoding.UTF8);
    parser.SetDelimiters(",");
    parser.HasFieldsEnclosedInQuotes = true;
    _ = parser.ReadFields();
    while (!parser.EndOfData)
    {
        var columns = parser.ReadFields();
        if (columns is null || columns.Length < 2 || !ids.Add(columns[1]))
            throw new InvalidDataException($"Duplicate or missing parcel ID in {Path.GetFileName(path)}.");
    }
    return ids;
}

static async Task<long> CountIds(NpgsqlConnection connection, NpgsqlTransaction transaction, HashSet<string> ids)
{
    await using var query = new NpgsqlCommand("SELECT count(*) FROM land_intelligence.land_parcels WHERE \"CadastralNumber\"=ANY(@ids)", connection, transaction);
    query.Parameters.AddWithValue("ids", ids.ToArray());
    return Convert.ToInt64(await query.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

static async Task<long> CountDesignations(NpgsqlConnection connection, NpgsqlTransaction transaction, string datasetKey)
{
    await using var query = new NpgsqlCommand(
        "SELECT count(*) FROM land_intelligence.colombo_state_research_project_lease_ready WHERE \"DatasetKey\"=@dataset",
        connection, transaction);
    query.Parameters.AddWithValue("dataset", datasetKey);
    return Convert.ToInt64(await query.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

internal sealed record ParcelRow(string Id, string RecordId, string ObjectId, string? ParcelId2,
    string? SheetId, string? GnIndex, string LandUseCode, string LandUseLabel, string Group,
    string Basis, string OverlapJson, string EvidenceJson, decimal Area, double Lat, double Lon,
    double MarkerLat, double MarkerLon, DateTimeOffset VerifiedAt, string GeometryJson);
