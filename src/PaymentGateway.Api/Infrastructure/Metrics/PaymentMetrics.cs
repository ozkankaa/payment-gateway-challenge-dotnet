using System.Diagnostics.Metrics;

namespace PaymentGateway.Api.Infrastructure.Metrics
{
    public sealed class PaymentMetrics
    {
        private static readonly Meter Meter = new("payment-service");

        public static readonly Counter<long> PaymentsTotal = Meter.CreateCounter<long>("payments_total");

        public static void AddPaymentTotal(string operation, string status)
        {
            PaymentsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("status", status));
        }
    }
}
