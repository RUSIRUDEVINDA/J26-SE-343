using System.Threading;
using System.Threading.Tasks;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application service representing the execution boundary for the Regulatory Compliance Engine.
/// </summary>
public interface IRegulatoryComplianceService
{
    /// <summary>
    /// Evaluates compliance for a specific land action.
    /// </summary>
    Task<bool> EvaluateComplianceAsync(string actionDetails, CancellationToken cancellationToken = default);
}
