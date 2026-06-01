
using System.Text.Json;

using PaymentGateway.Api.Application.Abstractions.CQRS;
using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents.PaymentCaptured;
using PaymentGateway.Api.Domain.Events.PaymentCaptured;
using PaymentGateway.Api.Domain.Entities.Outbox;

namespace PaymentGateway.Api.Application.Features.Payments.DomainEvents.PaymentCaptured
{
    public class PaymentCapturedDomainEventHandler(IOutboxEventRepository repository) : ICommandHandler<PaymentCapturedDomainEvent, bool>
    {
        public async Task<bool> HandleAsync(PaymentCapturedDomainEvent command, CancellationToken cancellationToken)
        {
            var paymentCapturedIntegrationEvent = new PaymentCapturedIntegrationEvent(
                    command.PaymentId,
                    command.MerchantId);

            var content = JsonSerializer.Serialize(paymentCapturedIntegrationEvent, paymentCapturedIntegrationEvent.GetType());

            var outboxEvent = OutboxEvent.Create(nameof(PaymentCapturedIntegrationEvent), content);

            await repository.AddAsync(outboxEvent, cancellationToken);

            return true;
        }
    }
}
