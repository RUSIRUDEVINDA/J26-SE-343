namespace StateLandGovernance.BuildingBlocks.CQRS;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

public class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public QueryDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResult> DispatchAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResult>
    {
        var handler = _serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.HandleAsync(query, cancellationToken);
    }
}
