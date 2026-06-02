using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PaymentGateway.Api.Saga.Entities;

public class PaymentSagaMap :
    SagaClassMap<PaymentSagaState>
{
    protected override void Configure(
        EntityTypeBuilder<PaymentSagaState> entity,
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
