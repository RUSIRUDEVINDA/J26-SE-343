namespace StateLandGovernance.BuildingBlocks.CQRS;

using System.Threading;
using System.Threading.Tasks;

public interface ICommandDispatcher
{
    Task DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;
}
