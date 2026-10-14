namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
