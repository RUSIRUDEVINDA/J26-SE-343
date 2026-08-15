namespace StateLandGovernance.BuildingBlocks.Events;

using System.Threading;
using System.Threading.Tasks;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
