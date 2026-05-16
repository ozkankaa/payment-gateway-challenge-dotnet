using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.Common;

namespace PaymentGateway.Api.Tests.Common;

public class PaymentGatewayFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(s => s.ServiceType == typeof(IAcquiringBankClient));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddSingleton<IAcquiringBankClient, FakeAcquiringBankClient>();
        });
    }
}
