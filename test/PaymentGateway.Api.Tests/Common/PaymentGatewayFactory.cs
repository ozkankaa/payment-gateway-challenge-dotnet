using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Infrastructure.Services.AcquiringBankService;
using PaymentGateway.Api.Infrastructure.Services.FraudService;

namespace PaymentGateway.Api.Tests.Common;

public class PaymentGatewayFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var acquiringBankClientDescriptor = services.SingleOrDefault(s => s.ServiceType == typeof(IAcquiringBankClient));
            if (acquiringBankClientDescriptor is not null) services.Remove(acquiringBankClientDescriptor);
            services.AddSingleton<IAcquiringBankClient, FakeAcquiringBankClient>();

            var fraudServiceClientDescriptor = services.SingleOrDefault(s => s.ServiceType == typeof(IFraudServiceClient));
            if (fraudServiceClientDescriptor is not null) services.Remove(fraudServiceClientDescriptor);
            services.AddSingleton<IFraudServiceClient, FakeFraudServiceClient>();
        });
    }
}
