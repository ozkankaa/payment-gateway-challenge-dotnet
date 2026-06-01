using System.Reflection.Emit;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PaymentGateway.Api.Domain.Entities.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => new { x.MerchantId, x.IdempotencyKey })
            .IsUnique();

        builder.OwnsOne(x => x.CardDetails, card =>
        {
            card.Property(x => x.LastFour)
                .HasColumnName("CardNumberLastFour")
                .HasMaxLength(4)
                .IsRequired();

            card.Property(x => x.ExpiryMonth)
                .HasColumnName("ExpiryMonth")
                .IsRequired();

            card.Property(x => x.ExpiryYear)
                .HasColumnName("ExpiryYear")
                .IsRequired();
        });

        builder.OwnsOne(x => x.Money, money =>
        {
            money.Property(x => x.Amount)
                .HasColumnName("Amount")
                .IsRequired();

            money.Property(x => x.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(x => x.ProviderReference, provider =>
        {
            provider.Property(x => x.ProviderId)
                .HasColumnName("ProviderId")
                .HasMaxLength(200);

            provider.Property(x => x.ProviderToken)
                .HasColumnName("ProviderToken")
                .HasMaxLength(500);
        });

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Ignore(x => x.DomainEvents);
    }
}
