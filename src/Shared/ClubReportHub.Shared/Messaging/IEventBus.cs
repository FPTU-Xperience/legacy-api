using ClubReportHub.Shared.Events;

namespace ClubReportHub.Shared.Messaging;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;
}
