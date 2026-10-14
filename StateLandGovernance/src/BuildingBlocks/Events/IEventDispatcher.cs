namespace StateLandGovernance.BuildingBlocks.Events;

using System.Threading;
using System.Threading.Tasks;

public interface IEventDispatcher
{
    Task PublishDomainEventAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
    Task PublishIntegrationEventAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent;
}
