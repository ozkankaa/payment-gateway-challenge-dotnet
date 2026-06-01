using PaymentGateway.Api.Tests.Integration.Messaging.RabbitMQ;

namespace PaymentGateway.Api.Tests.Infrastructure.Messaging.RabbitMQ;

[CollectionDefinition(nameof(RabbitMqTestCollection))]
public sealed class RabbitMqTestCollection
    : ICollectionFixture<RabbitMqTestFixture>
{
}