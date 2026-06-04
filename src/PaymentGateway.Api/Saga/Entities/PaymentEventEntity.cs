using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PaymentGateway.Api.Saga.Event;

namespace PaymentGateway.Api.Saga.Entities;

public class PaymentEventEntity :
    SagaClassMap<PaymentEventState>
{
    protected override void Configure(
        EntityTypeBuilder<PaymentEventState> entity,
        ModelBuilder model)
    {
        entity.Property(x => x.CurrentState)
            .HasMaxLength(64);

        entity.Property(x => x.MerchantId)
            .HasMaxLength(100);

        entity.Property(x => x.Currency)
            .HasMaxLength(10);
    }
}
