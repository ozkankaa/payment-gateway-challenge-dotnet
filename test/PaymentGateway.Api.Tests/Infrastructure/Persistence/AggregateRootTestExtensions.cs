using System.Reflection;

using PaymentGateway.Api.Domain.Abstractions;

internal static class AggregateRootTestExtensions
{
    public static void RaiseTestDomainEvent(
        this AggregateRoot aggregateRoot,
        IDomainEvent domainEvent)
    {
        var method = typeof(AggregateRoot)
            .GetMethod(
                "RaiseDomainEvent",
                BindingFlags.Instance | BindingFlags.NonPublic);

        method!.Invoke(aggregateRoot, new object[] { domainEvent });
    }
}