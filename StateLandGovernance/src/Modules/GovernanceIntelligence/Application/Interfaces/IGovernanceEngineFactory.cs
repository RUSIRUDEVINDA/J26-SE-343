using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Service factory to resolve the appropriate IGovernanceEngine implementation.
/// </summary>
public interface IGovernanceEngineFactory
{
    /// <summary>
    /// Resolves the engine implementation corresponding to the specified EngineType.
    /// </summary>
    IGovernanceEngine GetEngine(EngineType engineType);
}
