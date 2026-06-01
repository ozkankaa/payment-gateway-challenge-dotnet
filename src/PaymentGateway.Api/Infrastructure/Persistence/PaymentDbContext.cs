using Microsoft.EntityFrameworkCore;

using PaymentGateway.Api.Domain.Entities.Outbox;
using PaymentGateway.Api.Domain.Entities.Payments;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            modelBuilder.Entity<Payment>()
                .Property(x => x.Version)
                .HasDefaultValueSql("randomblob(8)")
                .ValueGeneratedOnAdd(); ;
        }
        else
        {
            modelBuilder.Entity<Payment>()
                .Property(x => x.Version)
                .IsRowVersion();
        }

        base.OnModelCreating(modelBuilder);
    }
}