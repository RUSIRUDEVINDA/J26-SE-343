using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// PostGIS-backed validation for imported GIS reference layers.
/// </summary>
public interface IGisReferenceDataValidationService
{
    Task<GisReferenceDataValidationResult> ValidateHambantotaPilotAsync(
        CancellationToken cancellationToken = default);
}
