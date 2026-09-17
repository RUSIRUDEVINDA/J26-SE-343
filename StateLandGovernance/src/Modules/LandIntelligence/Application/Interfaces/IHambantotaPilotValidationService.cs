using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// End-to-end Hambantota pilot validation for Component 1 (H9 enrichment through H12 recommendation).
/// </summary>
public interface IHambantotaPilotValidationService
{
    Task<HambantotaPilotValidationScenarioResult> ValidateScenarioAsync(
        HambantotaPilotValidationRequest request,
        CancellationToken cancellationToken = default);

    Task<HambantotaPilotValidationReport> ValidateScenariosAsync(
        IReadOnlyList<HambantotaPilotValidationRequest> requests,
        CancellationToken cancellationToken = default);
}
