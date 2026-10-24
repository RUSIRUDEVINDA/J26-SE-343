using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

public sealed class Colombo400GnDivisionMappingImportService : IParcelGnDivisionMappingImportService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILogger<Colombo400GnDivisionMappingImportService> _logger;

    public Colombo400GnDivisionMappingImportService(
        LandIntelligenceDbContext dbContext,
        ILogger<Colombo400GnDivisionMappingImportService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ParcelGnDivisionMappingImportReport> EnsureImportedAsync(
        CancellationToken cancellationToken = default)
    {
        var csvPath = Colombo400GnDivisionMappingData.ResolveCsvPath();
        var csvText = await File.ReadAllTextAsync(csvPath, cancellationToken);
        var workbookSha256 = Colombo400GnDivisionMappingCsv.ComputeSha256(csvText);
        if (!string.Equals(workbookSha256, Colombo400GnDivisionMappingCsv.ExpectedWorkbookSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"GN division mapping workbook hash mismatch. Expected {Colombo400GnDivisionMappingCsv.ExpectedWorkbookSha256}, got {workbookSha256}.");
        }

        var rows = Colombo400GnDivisionMappingCsv.Parse(csvText);
        var validation = Colombo400GnDivisionMappingCsv.Validate(rows);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"GN division mapping workbook failed validation. Rows={validation.RowCount}, distinct cadastral={validation.DistinctCadastralCount}.");
        }

        var cadastralNumbers = rows.Select(row => row.CadastralNumber).ToList();
        var parcelIds = await _dbContext.LandParcels
            .AsNoTracking()
            .Where(parcel => cadastralNumbers.Contains(parcel.CadastralNumber))
            .Select(parcel => new { parcel.Id, parcel.CadastralNumber })
            .ToListAsync(cancellationToken);
        var parcelIdByCadastral = parcelIds.ToDictionary(
            row => row.CadastralNumber,
            row => row.Id,
            StringComparer.Ordinal);

        var existing = await _dbContext.ParcelGnDivisionMappings
            .Where(row => cadastralNumbers.Contains(row.CadastralNumber))
            .ToDictionaryAsync(row => row.CadastralNumber, StringComparer.Ordinal, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var upserted = 0;
        var linked = 0;
        foreach (var row in rows)
        {
            parcelIdByCadastral.TryGetValue(row.CadastralNumber, out var parcelId);
            if (parcelId != Guid.Empty)
            {
                linked++;
            }

            if (!existing.TryGetValue(row.CadastralNumber, out var entity))
            {
                entity = new ParcelGnDivisionMappingEntity
                {
                    CadastralNumber = row.CadastralNumber,
                    ImportedAtUtc = now,
                };
                _dbContext.ParcelGnDivisionMappings.Add(entity);
            }

            entity.LandParcelId = parcelId == Guid.Empty ? null : parcelId;
            entity.DatasetKey = row.DatasetKey;
            entity.SourceGnIndex = row.SourceGnIndex;
            entity.Status = row.Status;
            entity.WorkbookStatusLabel = row.WorkbookStatusLabel;
            entity.SourceCodeGnName = row.SourceCodeGnName;
            entity.SourceCodeGnNumber = row.SourceCodeGnNumber;
            entity.SourceCodeDs = row.SourceCodeDs;
            entity.CodeDistrict = row.CodeDistrict;
            entity.MarkerGnName = row.MarkerGnName;
            entity.MarkerGnNumber = row.MarkerGnNumber;
            entity.MarkerDs = row.MarkerDs;
            entity.MarkerLatitude = row.MarkerLatitude;
            entity.MarkerLongitude = row.MarkerLongitude;
            entity.WorkbookSha256 = workbookSha256;
            entity.UpdatedAtUtc = now;
            upserted++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Colombo 400 GN division mapping import completed. Upserted={Upserted}, linked={Linked}, unlinked={Unlinked}",
            upserted,
            linked,
            rows.Count - linked);

        return new ParcelGnDivisionMappingImportReport(
            "upsert",
            rows.Count,
            upserted,
            linked,
            rows.Count - linked,
            workbookSha256,
            now);
    }
}

internal static class Colombo400GnDivisionMappingData
{
    public static string ResolveCsvPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Persistence", "RealParcels", "Data", "Colombo_400_GN_Division_Mapping.csv"),
            Path.Combine(AppContext.BaseDirectory, "Data", "Colombo_400_GN_Division_Mapping.csv"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && dir is not null; depth++)
        {
            var fromSource = Path.Combine(
                dir.FullName,
                "src",
                "Modules",
                "LandIntelligence",
                "Infrastructure",
                "Persistence",
                "RealParcels",
                "Data",
                "Colombo_400_GN_Division_Mapping.csv");
            if (File.Exists(fromSource))
            {
                return fromSource;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Colombo_400_GN_Division_Mapping.csv was not found for GN division import.");
    }
}
