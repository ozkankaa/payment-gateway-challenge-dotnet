using Microsoft.EntityFrameworkCore;

using PaymentGateway.Api.Grpc;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Middleware;

using Serilog;

namespace PaymentGateway.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseAppPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseSerilogRequestLogging();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapGrpcReflectionService();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Gateway API v1");
                options.SwaggerEndpoint("/swagger/v2/swagger.json", "Payment Gateway API v2 - Saga");
                options.RoutePrefix = "swagger";
            });
        }
        else
        {
            app.UseExceptionHandler();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors("Merchants");
        app.UseRateLimiter();
        app.UseOutputCache();
        app.UseAuthorization();

        app.MapHealthChecksEndpoints();

        app.MapControllers();

        app.MapGrpcService<PaymentGrpcV1Service>();

        using (var scope = app.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            dbContext.Database.MigrateAsync();
        }

        return app;
    }
}
