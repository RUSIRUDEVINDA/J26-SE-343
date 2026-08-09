namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
