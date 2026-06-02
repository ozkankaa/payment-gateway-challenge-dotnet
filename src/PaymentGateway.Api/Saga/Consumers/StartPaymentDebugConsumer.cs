using MassTransit;

namespace PaymentGateway.Api.Saga.Consumers;

public class StartPaymentDebugConsumer : IConsumer<StartPayment>
{
    public Task Consume(ConsumeContext<StartPayment> context)
    {
        Console.WriteLine("DEBUG CONSUMER RECEIVED StartPayment");
        Console.WriteLine($"CorrelationId: {context.Message.CorrelationId}");

        return Task.CompletedTask;
    }
}
