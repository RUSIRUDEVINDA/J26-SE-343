using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application abstraction for reading summarized dashboard metrics and persisted case assessments.
/// </summary>
public interface IGovernanceDashboardReader
{
    Task<GovernanceDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

