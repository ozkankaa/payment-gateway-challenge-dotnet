using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentGateway.Api.Infrastructure;

public sealed class IdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) return;
        if (!context.ApiDescription.RelativePath?.Contains("payments", StringComparison.OrdinalIgnoreCase) ?? true) return;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional stable key to safely retry payment creation without double-charging.",
            Schema = new OpenApiSchema { Type = "string", MinLength = 8, MaxLength = 128 }
        });
    }
}

