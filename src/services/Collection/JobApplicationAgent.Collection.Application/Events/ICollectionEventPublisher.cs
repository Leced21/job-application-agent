using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Events;

public interface ICollectionEventPublisher
{
    Task PublishAsync(CollectionOutboxMessage message, CancellationToken cancellationToken);
}
