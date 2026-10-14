using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Domain.Services;

namespace StateLandGovernance.LeaseFeasibility.Application.Commands;

/// <summary>
/// Command to predict the approval probability of a lease based on financial records.
/// </summary>
public sealed record PredictLeaseApprovalCommand(
    string ApplicationId,
    FinancialProfileDto Input
);

/// <summary>
/// Handler for the PredictLeaseApprovalCommand.
/// </summary>
public sealed class PredictLeaseApprovalCommandHandler
{
    private readonly ILeaseApprovalPredictionEngine _predictionEngine;
    private readonly TimeProvider _timeProvider;

    public PredictLeaseApprovalCommandHandler(
        ILeaseApprovalPredictionEngine predictionEngine,
        TimeProvider timeProvider)
    {
        _predictionEngine = predictionEngine ?? throw new ArgumentNullException(nameof(predictionEngine));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<ApprovalPredictionDto> HandleAsync(PredictLeaseApprovalCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Lease approval prediction is not yet implemented.");
    }
}
