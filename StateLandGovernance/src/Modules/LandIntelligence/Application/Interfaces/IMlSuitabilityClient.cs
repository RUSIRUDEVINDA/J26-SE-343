using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public sealed record MlSuitabilityPrediction(
    string PredictedLabel,
    IReadOnlyDictionary<string, decimal> Probabilities);

public interface IMlSuitabilityClient
{
    Task<MlSuitabilityPrediction?> PredictAsync(
        LandParcel parcel,
        LandUseType requestedPurpose,
        CancellationToken cancellationToken = default);
}
