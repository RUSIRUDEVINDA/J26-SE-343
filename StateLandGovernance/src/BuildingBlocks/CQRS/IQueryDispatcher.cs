namespace StateLandGovernance.BuildingBlocks.CQRS;

using System.Threading;
using System.Threading.Tasks;

public interface IQueryDispatcher
{
    Task<TResult> DispatchAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResult>;
}
