namespace StateLandGovernance.BuildingBlocks.CQRS;

using System.Threading;
using System.Threading.Tasks;

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
