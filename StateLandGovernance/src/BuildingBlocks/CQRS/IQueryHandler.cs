namespace StateLandGovernance.BuildingBlocks.CQRS;

using System.Threading;
using System.Threading.Tasks;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
