using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
using StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

public sealed class ColomboStateResearch300ImportService
{
    public const string ExpectedDatabaseName = "land_intel_colombo_state_research";
    public const string ConnectionEnvironmentVariable = "LAND_INTELLIGENCE_STATE_RESEARCH_CONNECTION";

    private const int Srid = 4326;
    private const int ExpectedParcelCount = 300;

    private readonly ILogger<ColomboStateResearch300ImportService> _logger;
    private readonly GeoJsonReader _geoJsonReader = new();

    public ColomboStateResearch300ImportService(ILogger<ColomboStateResearch300ImportService> logger) =>
        _logger = logger;

    public async Task<ColomboStateResearch300ImportReport> RunAsync(
        ColomboStateResearch300ImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var startedAt = DateTimeOffset.UtcNow;
        var skipReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var failed = 0;

        var csvPath = Path.GetFullPath(options.ParcelsCsv);
        var geoJsonPath = Path.GetFullPath(options.BoundariesGeoJson);
        var scenarioPath = Path.GetFullPath(options.SimulatedLeaseCsv);

        var csvHash = await ComputeFileHashAsync(csvPath, cancellationToken);
        var geoHash = await ComputeFileHashAsync(geoJsonPath, cancellationToken);
        var scenarioHash = await ComputeFileHashAsync(scenarioPath, cancellationToken);

        var csvText = await File.ReadAllTextAsync(csvPath, Encoding.UTF8, cancellationToken);
        var csvRows = ColomboStateResearch300CsvParser.Parse(csvText);
        if (csvRows.Count < 2)
        {
            throw new InvalidDataException("Parcel CSV must include a header row and at least one data row.");
        }

        var columnIndex = ColomboStateResearch300CsvParser.ReadHeader(csvRows[0]);
        var csvRecords = new Dictionary<string, ResearchParcelCsvRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in csvRows.Skip(1))
        {
            var recordId = ColomboStateResearch300CsvParser.GetField(row, columnIndex, "record_id");
            var parcelSourceId = ColomboStateResearch300CsvParser.GetField(row, columnIndex, "parcel_source_id");
            if (string.IsNullOrWhiteSpace(recordId) || string.IsNullOrWhiteSpace(parcelSourceId))
            {
                Count(skipReasons, "csv-missing-ids");
                continue;
            }

            if (!csvRecords.TryAdd(recordId, MapCsvRow(row, columnIndex, recordId, parcelSourceId)))
            {
                Count(skipReasons, "csv-duplicate-record-id");
            }
        }

        var geoRoot = JObject.Parse(await File.ReadAllTextAsync(geoJsonPath, cancellationToken));
        var geoFeatures = geoRoot["features"]?.Children<JObject>().ToList()
            ?? throw new InvalidDataException("GeoJSON features collection is missing.");

        var geoByRecordId = new Dictionary<string, (JObject Feature, MultiPolygon Boundary)>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in geoFeatures)
        {
            var properties = feature["properties"] as JObject;
            var recordId = properties?["record_id"]?.ToString()?.Trim();
            var parcelSourceId = properties?["parcel_source_id"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(recordId) || string.IsNullOrWhiteSpace(parcelSourceId))
            {
                Count(skipReasons, "geojson-missing-ids");
                continue;
            }

            if (!geoByRecordId.TryAdd(recordId, (feature, ReadBoundaryGeometry(feature))))
            {
                Count(skipReasons, "geojson-duplicate-record-id");
            }
        }

        ValidateIdSets(csvRecords, geoByRecordId, skipReasons);

        var accepted = new List<ColomboStateResearch300ImportRow>(ExpectedParcelCount);
        foreach (var (recordId, csvRow) in csvRecords)
        {
            if (!geoByRecordId.TryGetValue(recordId, out var geo))
            {
                Count(skipReasons, "csv-record-missing-from-geojson");
                failed++;
                continue;
            }

            var properties = (JObject)geo.Feature["properties"]!;
            if (!string.Equals(
                    csvRow.ParcelSourceId,
                    properties["parcel_source_id"]?.ToString()?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                Count(skipReasons, "csv-geojson-parcel-source-id-mismatch");
                failed++;
                continue;
            }

            if (geo.Boundary.IsEmpty)
            {
                Count(skipReasons, "empty-boundary");
                failed++;
                continue;
            }

            var fingerprint = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{geoHash}:{recordId}:{csvRow.ParcelSourceId}"))).ToLowerInvariant();

            accepted.Add(new ColomboStateResearch300ImportRow(
                recordId,
                csvRow.ParcelSourceId,
                csvRow.SourceObjectId,
                csvRow.ParcelSourceId2,
                csvRow.SheetSourceId,
                csvRow.SourceName,
                csvRow.SourceLayer,
                csvRow.SourceUrl,
                csvRow.District,
                csvRow.Province,
                csvRow.SourceGnIndex,
                csvRow.SourceClaimantCategoryCode,
                csvRow.SourceClaimantCategoryLabel,
                csvRow.SourceLandUseCode,
                csvRow.SourceLandUseLabel,
                csvRow.SourceLandUseText,
                csvRow.AreaHectares,
                csvRow.CentroidLatitude,
                csvRow.CentroidLongitude,
                csvRow.MapMarkerLatitude,
                csvRow.MapMarkerLongitude,
                csvRow.CoordinateStatus,
                csvRow.CandidateReviewStatus,
                csvRow.OwnershipVerificationStatus,
                fingerprint,
                geo.Boundary));
        }

        if (accepted.Count != ExpectedParcelCount)
        {
            throw new InvalidDataException(
                $"Expected {ExpectedParcelCount} importable parcels after validation, but found {accepted.Count}.");
        }

        var scenarioRows = await ReadScenarioRowsAsync(scenarioPath, cancellationToken);
        if (scenarioRows.Count != ExpectedParcelCount)
        {
            throw new InvalidDataException(
                $"Simulated lease CSV must contain {ExpectedParcelCount} rows; found {scenarioRows.Count}.");
        }

        foreach (var scenario in scenarioRows)
        {
            if (!csvRecords.ContainsKey(scenario.RecordId))
            {
                throw new InvalidDataException(
                    $"Simulated lease row references unknown record_id '{scenario.RecordId}'.");
            }
        }

        ApplyResult? applied = null;
        EnrichmentApplyResult? enrichment = null;
        if (options.Apply)
        {
            applied = await ApplyAsync(
                options.ConnectionString!,
                accepted,
                scenarioRows,
                csvHash,
                geoHash,
                scenarioHash,
                cancellationToken);

            if (options.RunGisEnrichment)
            {
                enrichment = await RunGisEnrichmentPlaceholderAsync(
                    options,
                    applied.DatabaseParcelCount ?? 0,
                    cancellationToken);
            }
        }

        var report = new ColomboStateResearch300ImportReport(
            ExpectedDatabaseName,
            csvPath,
            geoJsonPath,
            scenarioPath,
            csvHash,
            geoHash,
            scenarioHash,
            options.Apply ? "apply" : "dry-run",
            csvRecords.Count,
            geoByRecordId.Count,
            accepted.Count,
            applied?.Inserted ?? 0,
            applied?.Updated ?? 0,
            skipReasons.Values.Sum(),
            failed,
            skipReasons,
            applied?.DatabaseParcelCount,
            applied?.SourceMetadataCount,
            applied?.PolygonCount,
            applied?.SimulatedLeaseCount,
            enrichment,
            startedAt,
            DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(options.ReportPath))
        {
            var reportPath = Path.GetFullPath(options.ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(
                reportPath,
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                }),
                cancellationToken);
        }

        _logger.LogInformation(
            "Colombo state research 300 import {Mode}: accepted={Accepted}, inserted={Inserted}, updated={Updated}",
            report.Mode,
            report.ValidParcels,
            report.Inserted,
            report.Updated);

        return report;
    }

    private static void ValidateIdSets(
        Dictionary<string, ResearchParcelCsvRow> csvRecords,
        Dictionary<string, (JObject Feature, MultiPolygon Boundary)> geoByRecordId,
        Dictionary<string, int> skipReasons)
    {
        if (csvRecords.Count != ExpectedParcelCount)
        {
            throw new InvalidDataException(
                $"Parcel CSV must contain {ExpectedParcelCount} unique record_id values; found {csvRecords.Count}.");
        }

        if (geoByRecordId.Count != ExpectedParcelCount)
        {
            throw new InvalidDataException(
                $"GeoJSON must contain {ExpectedParcelCount} unique record_id values; found {geoByRecordId.Count}.");
        }

        var csvParcelIds = csvRecords.Values.Select(row => row.ParcelSourceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var geoParcelIds = geoByRecordId.Values
            .Select(item => item.Feature["properties"]?["parcel_source_id"]?.ToString()?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (csvParcelIds.Count != ExpectedParcelCount || geoParcelIds.Count != ExpectedParcelCount)
        {
            throw new InvalidDataException("CSV and GeoJSON must each contain 300 unique parcel_source_id values.");
        }

        foreach (var id in csvParcelIds)
        {
            if (!geoParcelIds.Contains(id))
            {
                Count(skipReasons, "parcel-source-id-set-mismatch");
            }
        }

        foreach (var recordId in csvRecords.Keys)
        {
            if (!geoByRecordId.ContainsKey(recordId))
            {
                throw new InvalidDataException($"record_id '{recordId}' is missing from GeoJSON.");
            }
        }
    }

    private MultiPolygon ReadBoundaryGeometry(JObject feature)
    {
        var geometry = _geoJsonReader.Read<Geometry>(feature["geometry"]?.ToString(Formatting.None) ?? "null");
        MultiPolygon boundary = geometry switch
        {
            Polygon polygon => polygon.Factory.CreateMultiPolygon([polygon]),
            MultiPolygon multiPolygon => multiPolygon,
            _ => throw new InvalidDataException("Boundary geometry must be Polygon or MultiPolygon."),
        };
        boundary.SRID = Srid;
        return boundary;
    }

    private static ResearchParcelCsvRow MapCsvRow(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnIndex,
        string recordId,
        string parcelSourceId)
    {
        static string F(IReadOnlyList<string> r, IReadOnlyDictionary<string, int> c, string n) =>
            ColomboStateResearch300CsvParser.GetField(r, c, n);

        var areaText = F(row, columnIndex, "area_hectares");
        if (!decimal.TryParse(areaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var areaHectares))
        {
            throw new InvalidDataException($"Invalid area_hectares for record_id '{recordId}'.");
        }

        if (!double.TryParse(F(row, columnIndex, "centroid_latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var centroidLat)
            || !double.TryParse(F(row, columnIndex, "centroid_longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var centroidLon))
        {
            throw new InvalidDataException($"Invalid centroid coordinates for record_id '{recordId}'.");
        }

        if (!double.TryParse(F(row, columnIndex, "map_marker_latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var markerLat)
            || !double.TryParse(F(row, columnIndex, "map_marker_longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var markerLon))
        {
            throw new InvalidDataException($"Invalid map marker coordinates for record_id '{recordId}'.");
        }

        return new ResearchParcelCsvRow(
            recordId,
            parcelSourceId,
            NullIfEmpty(F(row, columnIndex, "source_object_id")),
            NullIfEmpty(F(row, columnIndex, "parcel_source_id2")),
            NullIfEmpty(F(row, columnIndex, "sheet_source_id")),
            F(row, columnIndex, "district"),
            "Western",
            NullIfEmpty(F(row, columnIndex, "source_gn_index")),
            NullIfEmpty(F(row, columnIndex, "source_claimant_category_code")),
            NullIfEmpty(F(row, columnIndex, "source_claimant_category_label")),
            NullIfEmpty(F(row, columnIndex, "source_land_use_code")),
            NullIfEmpty(F(row, columnIndex, "source_land_use_label")),
            NullIfEmpty(F(row, columnIndex, "source_land_use_text")),
            areaHectares,
            centroidLat,
            centroidLon,
            markerLat,
            markerLon,
            NullIfEmpty(F(row, columnIndex, "geometry_status")),
            NullIfEmpty(F(row, columnIndex, "candidate_review_status")),
            NullIfEmpty(F(row, columnIndex, "ownership_verification_status")),
            F(row, columnIndex, "source_name"),
            F(row, columnIndex, "source_layer"),
            NullIfEmpty(F(row, columnIndex, "source_url")));
    }

    private static async Task<List<ResearchSimulatedLeaseCsvRow>> ReadScenarioRowsAsync(
        string scenarioPath,
        CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(scenarioPath, Encoding.UTF8, cancellationToken);
        var rows = ColomboStateResearch300CsvParser.Parse(text);
        var columnIndex = ColomboStateResearch300CsvParser.ReadHeader(rows[0]);
        var result = new List<ResearchSimulatedLeaseCsvRow>();
        foreach (var row in rows.Skip(1))
        {
            var recordId = ColomboStateResearch300CsvParser.GetField(row, columnIndex, "record_id");
            var status = ColomboStateResearch300CsvParser.GetField(row, columnIndex, "simulated_lease_status");
            var isSimulated = ColomboStateResearch300CsvParser.GetField(row, columnIndex, "is_simulated_lease_status");
            if (string.IsNullOrWhiteSpace(recordId) || string.IsNullOrWhiteSpace(status))
            {
                continue;
            }

            if (!bool.TryParse(isSimulated, out var simulatedFlag) || !simulatedFlag)
            {
                throw new InvalidDataException(
                    $"Simulated lease row for '{recordId}' must set is_simulated_lease_status=true.");
            }

            if (!status.StartsWith("SIMULATED_", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Simulated lease status for '{recordId}' must use SIMULATED_* prefix.");
            }

            result.Add(new ResearchSimulatedLeaseCsvRow(
                ColomboStateResearch300CsvParser.GetField(row, columnIndex, "scenario_id"),
                recordId,
                status,
                NullIfEmpty(ColomboStateResearch300CsvParser.GetField(row, columnIndex, "dataset_usage")),
                NullIfEmpty(ColomboStateResearch300CsvParser.GetField(row, columnIndex, "notice"))));
        }

        return result;
    }

    private async Task<ApplyResult> ApplyAsync(
        string connectionString,
        IReadOnlyList<ColomboStateResearch300ImportRow> rows,
        IReadOnlyList<ResearchSimulatedLeaseCsvRow> scenarioRows,
        string csvHash,
        string geoHash,
        string scenarioHash,
        CancellationToken cancellationToken)
    {
        AssertResearchDatabase(connectionString);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseNetTopologySuite();
        await using var dataSource = dataSourceBuilder.Build();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ColomboCadastreParcelImportServiceEnsureSourceTableAsync(connection, transaction, cancellationToken);
        await EnsureResearchTablesAsync(connection, transaction, cancellationToken);
        var lookupIds = await ReadLookupIdsAsync(connection, transaction, cancellationToken);
        var existing = await CountExistingAsync(connection, transaction, rows.Select(x => x.ParcelSourceId), cancellationToken);

        foreach (var item in rows)
        {
            var parcelId = await UpsertParcelAsync(connection, transaction, item, lookupIds, csvHash, geoHash, cancellationToken);
            await UpsertProjectLeaseReadyDesignationAsync(connection, transaction, parcelId, item.RecordId, cancellationToken);
        }

        foreach (var scenario in scenarioRows)
        {
            var parcelId = CreateDeterministicGuid($"{ResolveSourceLayer(rows, scenario.RecordId)}:{ResolveParcelSourceId(rows, scenario.RecordId)}");
            await UpsertSimulatedLeaseAsync(connection, transaction, parcelId, scenario, scenarioHash, cancellationToken);
        }

        const string verificationSql = """
            SELECT
              (SELECT count(*) FROM land_intelligence.land_parcels),
              (SELECT count(*) FROM land_intelligence.real_parcel_sources),
              (SELECT count(*) FROM land_intelligence.land_parcels WHERE "Boundary" IS NOT NULL),
              (SELECT count(*) FROM land_intelligence.research_simulated_lease_workflows);
            """;
        await using var verification = new NpgsqlCommand(verificationSql, connection, transaction);
        await using var verificationReader = await verification.ExecuteReaderAsync(cancellationToken);
        await verificationReader.ReadAsync(cancellationToken);
        var databaseCount = checked((int)verificationReader.GetInt64(0));
        var sourceCount = checked((int)verificationReader.GetInt64(1));
        var polygonCount = checked((int)verificationReader.GetInt64(2));
        var simulatedCount = checked((int)verificationReader.GetInt64(3));
        await verificationReader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);

        return new ApplyResult(rows.Count - existing, existing, databaseCount, sourceCount, polygonCount, simulatedCount);
    }

    private static string ResolveSourceLayer(IReadOnlyList<ColomboStateResearch300ImportRow> rows, string recordId) =>
        rows.First(row => string.Equals(row.RecordId, recordId, StringComparison.OrdinalIgnoreCase)).SourceLayer;

    private static string ResolveParcelSourceId(IReadOnlyList<ColomboStateResearch300ImportRow> rows, string recordId) =>
        rows.First(row => string.Equals(row.RecordId, recordId, StringComparison.OrdinalIgnoreCase)).ParcelSourceId;

    private static async Task EnsureResearchTablesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS land_intelligence.research_simulated_lease_workflows (
                "ResearchRecordId" varchar(120) PRIMARY KEY,
                "LandParcelId" uuid NOT NULL REFERENCES land_intelligence.land_parcels("Id") ON DELETE CASCADE,
                "ScenarioId" varchar(120) NOT NULL,
                "SimulatedLeaseStatus" varchar(80) NOT NULL,
                "IsSimulatedLeaseStatus" boolean NOT NULL DEFAULT true,
                "DatasetUsage" varchar(80),
                "Notice" text,
                "SourceScenarioSha256" char(64) NOT NULL,
                "ImportedAtUtc" timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_research_simulated_lease_workflows_parcel
                ON land_intelligence.research_simulated_lease_workflows ("LandParcelId");
            ALTER TABLE land_intelligence.real_parcel_sources
                ADD COLUMN IF NOT EXISTS "ResearchRecordId" varchar(120);
            ALTER TABLE land_intelligence.real_parcel_sources
                ADD COLUMN IF NOT EXISTS "MapMarkerLatitude" double precision;
            ALTER TABLE land_intelligence.real_parcel_sources
                ADD COLUMN IF NOT EXISTS "MapMarkerLongitude" double precision;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
        await using var leaseReady = new NpgsqlCommand(
            ColomboStateResearch300ProjectLeaseReadyPersistence.EnsureTableSql,
            connection,
            transaction);
        await leaseReady.ExecuteNonQueryAsync(ct);
    }

    private static async Task ColomboCadastreParcelImportServiceEnsureSourceTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct)
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
                "LandUseEnglishLabel" varchar(200), "ClaimantCategoryCode" varchar(100), "ClaimantCategoryEnglishLabel" varchar(200),
                "ResearchRecordId" varchar(120),
                "MapMarkerLatitude" double precision,
                "MapMarkerLongitude" double precision
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertSimulatedLeaseAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid landParcelId,
        ResearchSimulatedLeaseCsvRow scenario,
        string scenarioHash,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO land_intelligence.research_simulated_lease_workflows
            ("ResearchRecordId","LandParcelId","ScenarioId","SimulatedLeaseStatus","IsSimulatedLeaseStatus","DatasetUsage","Notice","SourceScenarioSha256","ImportedAtUtc")
            VALUES (@recordId,@parcelId,@scenarioId,@status,true,@usage,@notice,@hash,now())
            ON CONFLICT ("ResearchRecordId") DO UPDATE SET
            "LandParcelId"=EXCLUDED."LandParcelId",
            "ScenarioId"=EXCLUDED."ScenarioId",
            "SimulatedLeaseStatus"=EXCLUDED."SimulatedLeaseStatus",
            "IsSimulatedLeaseStatus"=true,
            "DatasetUsage"=EXCLUDED."DatasetUsage",
            "Notice"=EXCLUDED."Notice",
            "SourceScenarioSha256"=EXCLUDED."SourceScenarioSha256",
            "ImportedAtUtc"=now();
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("recordId", scenario.RecordId);
        command.Parameters.AddWithValue("parcelId", landParcelId);
        command.Parameters.AddWithValue("scenarioId", scenario.ScenarioId);
        command.Parameters.AddWithValue("status", scenario.SimulatedLeaseStatus);
        command.Parameters.AddWithValue("usage", (object?)scenario.DatasetUsage ?? DBNull.Value);
        command.Parameters.AddWithValue("notice", (object?)scenario.Notice ?? DBNull.Value);
        command.Parameters.AddWithValue("hash", scenarioHash);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertProjectLeaseReadyDesignationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid landParcelId,
        string researchRecordId,
        CancellationToken ct)
    {
        var (label, notice) = ColomboStateResearch300ProjectLeaseReadyPersistence.DesignationCopy();
        await using var command = new NpgsqlCommand(
            ColomboStateResearch300ProjectLeaseReadyPersistence.UpsertSql,
            connection,
            transaction);
        command.Parameters.AddWithValue("recordId", researchRecordId);
        command.Parameters.AddWithValue("parcelId", landParcelId);
        command.Parameters.AddWithValue("datasetKey", ColomboStateResearchProjectLeaseReady.DatasetKey);
        command.Parameters.AddWithValue("label", label);
        command.Parameters.AddWithValue("notice", notice);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid> UpsertParcelAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ColomboStateResearch300ImportRow item,
        LookupIds lookup,
        string csvHash,
        string geoHash,
        CancellationToken ct)
    {
        var id = CreateDeterministicGuid($"{item.SourceLayer}:{item.ParcelSourceId}");
        var centroid = ColomboStateResearch300CoordinateSemantics.ToStoredCentroid(
            item.CentroidLatitude,
            item.CentroidLongitude);

        const string parcelSql = """
            INSERT INTO land_intelligence.land_parcels
            ("Id","CadastralNumber","SurveyPlanReference","LandCategoryId","CurrentLandUseId","AreaValue","AreaUnit","Province","District","DivisionalSecretariat","GramaNiladhariDivision","Centroid","Boundary","SpatialReferenceSystemId")
            VALUES (@id,@number,@sheet,@category,NULL,@area,@unit,@province,@district,@ds,NULL,@centroid,@boundary,4326)
            ON CONFLICT ("CadastralNumber") DO UPDATE SET
            "SurveyPlanReference"=EXCLUDED."SurveyPlanReference", "CurrentLandUseId"=NULL,
            "AreaValue"=EXCLUDED."AreaValue", "AreaUnit"=EXCLUDED."AreaUnit", "Province"=EXCLUDED."Province",
            "District"=EXCLUDED."District", "DivisionalSecretariat"=EXCLUDED."DivisionalSecretariat",
            "GramaNiladhariDivision"=NULL, "Centroid"=EXCLUDED."Centroid",
            "Boundary"=EXCLUDED."Boundary", "SpatialReferenceSystemId"=4326 RETURNING "Id";
            """;
        await using var parcel = new NpgsqlCommand(parcelSql, connection, transaction);
        parcel.Parameters.AddWithValue("id", id);
        parcel.Parameters.AddWithValue("number", item.ParcelSourceId);
        parcel.Parameters.AddWithValue("sheet", (object?)item.SheetSourceId ?? DBNull.Value);
        parcel.Parameters.AddWithValue("category", lookup.OtherCategoryId);
        parcel.Parameters.AddWithValue("area", item.AreaHectares);
        parcel.Parameters.AddWithValue("unit", (int)AreaUnit.Hectares);
        parcel.Parameters.AddWithValue("province", item.Province);
        parcel.Parameters.AddWithValue("district", item.District);
        parcel.Parameters.AddWithValue("ds", "Unavailable");
        parcel.Parameters.AddWithValue("centroid", NpgsqlDbType.Geometry, centroid);
        parcel.Parameters.AddWithValue("boundary", NpgsqlDbType.Geometry, item.Boundary);
        var persistedId = (Guid)(await parcel.ExecuteScalarAsync(ct))!;

        const string sourceSql = """
            INSERT INTO land_intelligence.real_parcel_sources
            ("LandParcelId","SourceName","SourceLayer","SourceFeatureId","SourceFingerprint","ImportedAtUtc","SourceZipSha256","SourceLayerSha256","ParcelId2","SheetId2","ParcelStatus","LandUseCode","LandUseDescription","CoordinateStatus","GnIndex","ClaimantCategory","SourceObjectId","SourceGlobalId","LandUseEnglishLabel","ClaimantCategoryCode","ClaimantCategoryEnglishLabel","ResearchRecordId","MapMarkerLatitude","MapMarkerLongitude")
            VALUES (@parcelId,@sourceName,@sourceLayer,@featureId,@fingerprint,now(),@csvHash,@geoHash,@parcelId2,@sheetId2,@status,@landUseCode,@landUseText,@coordinateStatus,@gnIndex,@claimantCategoryLegacy,@objectId,NULL,@landUseEnglish,@claimantCategoryCode,@claimantCategoryEnglish,@researchRecordId,@mapMarkerLat,@mapMarkerLon)
            ON CONFLICT ("LandParcelId") DO UPDATE SET
            "SourceName"=EXCLUDED."SourceName","SourceLayer"=EXCLUDED."SourceLayer","SourceFeatureId"=EXCLUDED."SourceFeatureId",
            "SourceFingerprint"=EXCLUDED."SourceFingerprint","ImportedAtUtc"=now(),"SourceZipSha256"=EXCLUDED."SourceZipSha256","SourceLayerSha256"=EXCLUDED."SourceLayerSha256",
            "ParcelId2"=EXCLUDED."ParcelId2","SheetId2"=EXCLUDED."SheetId2","ParcelStatus"=EXCLUDED."ParcelStatus",
            "LandUseCode"=EXCLUDED."LandUseCode","LandUseDescription"=EXCLUDED."LandUseDescription","CoordinateStatus"=EXCLUDED."CoordinateStatus",
            "GnIndex"=EXCLUDED."GnIndex","ClaimantCategory"=EXCLUDED."ClaimantCategory","SourceObjectId"=EXCLUDED."SourceObjectId",
            "LandUseEnglishLabel"=EXCLUDED."LandUseEnglishLabel","ClaimantCategoryCode"=EXCLUDED."ClaimantCategoryCode",
            "ClaimantCategoryEnglishLabel"=EXCLUDED."ClaimantCategoryEnglishLabel","ResearchRecordId"=EXCLUDED."ResearchRecordId",
            "MapMarkerLatitude"=EXCLUDED."MapMarkerLatitude","MapMarkerLongitude"=EXCLUDED."MapMarkerLongitude";
            """;
        await using var source = new NpgsqlCommand(sourceSql, connection, transaction);
        void Add(string name, object? value) => source.Parameters.AddWithValue(name, value ?? DBNull.Value);
        Add("parcelId", persistedId);
        Add("sourceName", item.SourceName);
        Add("sourceLayer", item.SourceLayer);
        Add("featureId", item.ParcelSourceId);
        Add("fingerprint", item.SourceFingerprint);
        Add("csvHash", csvHash);
        Add("geoHash", geoHash);
        Add("parcelId2", item.ParcelSourceId2);
        Add("sheetId2", item.SheetSourceId);
        Add("status", item.CandidateReviewStatus);
        Add("landUseCode", item.SourceLandUseCode);
        Add("landUseText", item.SourceLandUseText);
        Add("coordinateStatus", item.CoordinateStatus);
        Add("gnIndex", item.SourceGnIndex);
        Add("claimantCategoryLegacy", item.SourceClaimantCategoryCode);
        Add("objectId", item.SourceObjectId);
        Add("landUseEnglish", item.SourceLandUseLabel);
        Add("claimantCategoryCode", item.SourceClaimantCategoryCode);
        Add("claimantCategoryEnglish", item.SourceClaimantCategoryLabel);
        Add("researchRecordId", item.RecordId);
        Add("mapMarkerLat", item.MapMarkerLatitude);
        Add("mapMarkerLon", item.MapMarkerLongitude);
        await source.ExecuteNonQueryAsync(ct);
        return persistedId;
    }

    private static async Task<LookupIds> ReadLookupIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct)
    {
        var categories = new Dictionary<int, Guid>();
        await using (var command = new NpgsqlCommand("SELECT \"Id\", \"Type\" FROM land_intelligence.land_categories", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                categories[reader.GetInt32(1)] = reader.GetGuid(0);
            }

        if (!categories.TryGetValue((int)LandCategoryType.Other, out var otherCategory))
        {
            throw new InvalidOperationException("The Other land category lookup is missing. Run database migrations/seed first.");
        }

        return new LookupIds(otherCategory);
    }

    private static async Task<int> CountExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<string> ids,
        CancellationToken ct)
    {
        var array = ids.ToArray();
        if (array.Length == 0)
        {
            return 0;
        }

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM land_intelligence.land_parcels WHERE \"CadastralNumber\" = ANY(@ids)",
            connection,
            transaction);
        command.Parameters.AddWithValue("ids", array);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    private Task<EnrichmentApplyResult> RunGisEnrichmentPlaceholderAsync(
        ColomboStateResearch300ImportOptions options,
        int parcelCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.GisDataRoot))
        {
            return Task.FromResult(new EnrichmentApplyResult(
                false,
                "Skipped — --gis-data-root was not supplied.",
                Array.Empty<string>(),
                0,
                0));
        }

        _logger.LogWarning(
            "GIS enrichment for the research database requires Colombo reference layers in {GisRoot}. " +
            "Import layers with ColomboDemoSetup or GisReferenceDataImport, then run enrichment per parcel. " +
            "This import records the dependency only.",
            options.GisDataRoot);

        return Task.FromResult(new EnrichmentApplyResult(
            false,
            "Not executed in this import pass — import Colombo GIS layers into the isolated database, add Colombo to LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames, then enrich parcels.",
            new[]
            {
                "gis_roads (OSM motor roads for Colombo)",
                "gis_water_features",
                "gis_soil_groups",
                "gis_soil_conservation_areas",
                "gis_soil_erosion_observations",
            },
            parcelCount,
            0));
    }

    private static void ValidateOptions(ColomboStateResearch300ImportOptions options)
    {
        if (!File.Exists(options.ParcelsCsv))
        {
            throw new FileNotFoundException("Parcel CSV was not found.", options.ParcelsCsv);
        }

        if (!File.Exists(options.BoundariesGeoJson))
        {
            throw new FileNotFoundException("Boundaries GeoJSON was not found.", options.BoundariesGeoJson);
        }

        if (!File.Exists(options.SimulatedLeaseCsv))
        {
            throw new FileNotFoundException("Simulated lease CSV was not found.", options.SimulatedLeaseCsv);
        }

        if (options.Apply)
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new ArgumentException("--apply requires an explicit connection string or environment variable.");
            }

            AssertResearchDatabase(options.ConnectionString);
        }
    }

    public static void AssertResearchDatabase(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (!string.Equals(databaseName, ExpectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to import into database '{databaseName}'. Expected '{ExpectedDatabaseName}'.");
        }

        if (string.Equals(databaseName, "state_land_governance", StringComparison.OrdinalIgnoreCase)
            || string.Equals(databaseName, "land_intel_colombo_real_demo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(databaseName, "land_intel_colombo_real_pilot", StringComparison.OrdinalIgnoreCase)
            || string.Equals(databaseName, "land_intel_colombo_demo", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to modify protected database '{databaseName}'.");
        }
    }

    private static Guid CreateDeterministicGuid(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);

    private static void Count(Dictionary<string, int> values, string key) =>
        values[key] = values.GetValueOrDefault(key) + 1;

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
    }

    private sealed record LookupIds(Guid OtherCategoryId);
}

public sealed record ColomboStateResearch300ImportOptions(
    string ParcelsCsv,
    string BoundariesGeoJson,
    string SimulatedLeaseCsv,
    bool Apply = false,
    string? ConnectionString = null,
    string? ReportPath = null,
    bool RunGisEnrichment = false,
    string? GisDataRoot = null);

public sealed record EnrichmentApplyResult(
    bool Executed,
    string Summary,
    IReadOnlyList<string> MissingLayers,
    int ParcelCount,
    int ParcelsEnriched);

public sealed record ColomboStateResearch300ImportReport(
    string TargetDatabase,
    string ParcelsCsv,
    string BoundariesGeoJson,
    string SimulatedLeaseCsv,
    string ParcelsCsvSha256,
    string BoundariesGeoJsonSha256,
    string SimulatedLeaseCsvSha256,
    string Mode,
    int CsvRecordCount,
    int GeoJsonFeatureCount,
    int ValidParcels,
    int Inserted,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyDictionary<string, int> SkipReasons,
    int? DatabaseParcelCount,
    int? SourceMetadataCount,
    int? PolygonCount,
    int? SimulatedLeaseCount,
    EnrichmentApplyResult? GisEnrichment,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

internal sealed record ResearchParcelCsvRow(
    string RecordId,
    string ParcelSourceId,
    string? SourceObjectId,
    string? ParcelSourceId2,
    string? SheetSourceId,
    string District,
    string Province,
    string? SourceGnIndex,
    string? SourceClaimantCategoryCode,
    string? SourceClaimantCategoryLabel,
    string? SourceLandUseCode,
    string? SourceLandUseLabel,
    string? SourceLandUseText,
    decimal AreaHectares,
    double CentroidLatitude,
    double CentroidLongitude,
    double MapMarkerLatitude,
    double MapMarkerLongitude,
    string? CoordinateStatus,
    string? CandidateReviewStatus,
    string? OwnershipVerificationStatus,
    string SourceName,
    string SourceLayer,
    string? SourceUrl);

internal sealed record ResearchSimulatedLeaseCsvRow(
    string ScenarioId,
    string RecordId,
    string SimulatedLeaseStatus,
    string? DatasetUsage,
    string? Notice);

public sealed record ColomboStateResearch300ImportRow(
    string RecordId,
    string ParcelSourceId,
    string? SourceObjectId,
    string? ParcelSourceId2,
    string? SheetSourceId,
    string SourceName,
    string SourceLayer,
    string? SourceUrl,
    string District,
    string Province,
    string? SourceGnIndex,
    string? SourceClaimantCategoryCode,
    string? SourceClaimantCategoryLabel,
    string? SourceLandUseCode,
    string? SourceLandUseLabel,
    string? SourceLandUseText,
    decimal AreaHectares,
    double CentroidLatitude,
    double CentroidLongitude,
    double MapMarkerLatitude,
    double MapMarkerLongitude,
    string? CoordinateStatus,
    string? CandidateReviewStatus,
    string? OwnershipVerificationStatus,
    string SourceFingerprint,
    MultiPolygon Boundary);

internal static class ColomboStateResearch300CoordinateSemantics
{
    public static Point ToStoredCentroid(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = 4326 };
}

internal sealed record ApplyResult(
    int Inserted,
    int Updated,
    int? DatabaseParcelCount,
    int? SourceMetadataCount,
    int? PolygonCount,
    int? SimulatedLeaseCount);
