using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;
using StateLandGovernance.LeaseFeasibility.Domain.Services;

namespace StateLandGovernance.LeaseFeasibility.Application.Commands;

/// <summary>
/// Command to assess the financial feasibility of a lease application.
/// </summary>
public sealed record AssessFinancialFeasibilityCommand(
    string ApplicationId,
    FinancialProfileDto Input
);

/// <summary>
/// Handler for the AssessFinancialFeasibilityCommand.
/// </summary>
public sealed class AssessFinancialFeasibilityCommandHandler
{
    private readonly IFinancialFeasibilityScoringEngine _scoringEngine;
    private readonly IFinancialFeasibilityRepository _repository;
    private readonly TimeProvider _timeProvider;

    public AssessFinancialFeasibilityCommandHandler(
        IFinancialFeasibilityScoringEngine scoringEngine,
        IFinancialFeasibilityRepository repository,
        TimeProvider timeProvider)
    {
        _scoringEngine = scoringEngine ?? throw new ArgumentNullException(nameof(scoringEngine));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<FeasibilityAssessmentDto> HandleAsync(AssessFinancialFeasibilityCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Financial feasibility assessment is not yet implemented.");
    }
}
