namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;

public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
