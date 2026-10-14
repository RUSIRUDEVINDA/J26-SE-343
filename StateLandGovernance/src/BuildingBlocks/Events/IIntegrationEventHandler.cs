namespace StateLandGovernance.BuildingBlocks.Events;

using System.Threading;
using System.Threading.Tasks;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}
