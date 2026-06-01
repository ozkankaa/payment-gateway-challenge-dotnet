using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PaymentGateway.Api.Domain.Entities.Outbox;

namespace PaymentGateway.Api.Infrastructure.Persistence.Configurations;

public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("OutboxEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired();

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc);

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.ProcessedAtUtc);
    }
}
