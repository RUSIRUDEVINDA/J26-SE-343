using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Imports externally sourced GIS reference layers into PostgreSQL/PostGIS tables.
/// </summary>
public interface IGisReferenceDataImportService
{
    Task<GisReferenceDataImportResult> ImportHambantotaPilotAsync(
        string? dataRootPath = null,
        CancellationToken cancellationToken = default);
}
