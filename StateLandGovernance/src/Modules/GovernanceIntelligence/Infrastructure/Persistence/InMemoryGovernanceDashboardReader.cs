using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation of IGovernanceDashboardReader for development/testing when PostgreSQL is unconfigured.
/// </summary>
public sealed class InMemoryGovernanceDashboardReader : IGovernanceDashboardReader
{
    public Task<GovernanceDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var summary = new GovernanceDashboardSummaryDto(
            TotalCases: 0,
            TotalAssessments: 0,
            ClearCount: 0,
            ReviewRequiredCount: 0,
            InsufficientInfoCount: 0,
            LastUpdated: DateTime.UtcNow.ToString("o"));

        return Task.FromResult(new GovernanceDashboardDto(
            summary,
            Array.Empty<GovernanceCaseAssessmentDto>()));
    }
}

