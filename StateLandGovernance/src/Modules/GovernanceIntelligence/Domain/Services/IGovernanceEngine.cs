using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service interface representing a governance intelligence sub-engine boundary.
/// </summary>
public interface IGovernanceEngine
{
    /// <summary>
    /// Gets the unique engine type represented by this implementation.
    /// </summary>
    EngineType EngineType { get; }
}
