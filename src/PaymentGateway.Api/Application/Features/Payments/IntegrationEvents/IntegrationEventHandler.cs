using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentAuthorized;
using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentCaptured;
using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentCreated;
using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentFailed;

namespace PaymentGateway.Api.Application.Features.Payments.IntegrationEvents
{
    public class IntegrationEventHandler(ILogger<IntegrationEventHandler> logger) : IIntegrationEventHandler
    {
        public Task HandleAsync(IntegrationEvent command, CancellationToken cancellationToken)
        {
            var messageType = command.MessageType;
            var content = command.Content;

            logger.LogInformation(
            "Consumed RabbitMQ domain event. Type: {MessageType}. Content: {Content}",
            messageType,
            content);

            if (messageType.EndsWith(nameof(PaymentCreatedIntegrationEvent), StringComparison.Ordinal))
            {
                logger.LogInformation("Handling PaymentCreatedIntegrationEvent.");
                return Task.CompletedTask;
            }

            if (messageType.EndsWith(nameof(PaymentAuthorizedIntegrationEvent), StringComparison.Ordinal))
            {
                logger.LogInformation("Handling PaymentAuthorizedIntegrationEvent.");
                return Task.CompletedTask;
            }

            if (messageType.EndsWith(nameof(PaymentCapturedIntegrationEvent), StringComparison.Ordinal))
            {
                logger.LogInformation("Handling PaymentCapturedIntegrationEvent.");
                return Task.CompletedTask;
            }

            if (messageType.EndsWith(nameof(PaymentFailedIntegrationEvent), StringComparison.Ordinal))
            {
                logger.LogInformation("Handling PaymentFailedIntegrationEvent.");
                return Task.CompletedTask;
            }

            logger.LogWarning(
                "Unknown RabbitMQ message type {MessageType}. Message skipped.",
                messageType);

            return Task.CompletedTask;
        }
    }
}
