using Serilog;

namespace PaymentGateway.Api.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplication BuildApp(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel((_, options) => options.Limits.MaxRequestBodySize = 1 * 1024 * 1024);

        builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(context.Configuration));

        builder.Services.AddServices(builder.Configuration);

        var app = builder.Build();

        app.UseAppPipeline();
        
        return app;
    }
}
