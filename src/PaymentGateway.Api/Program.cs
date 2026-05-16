using System.Reflection;
using System.Threading.RateLimiting;

using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi.Models;

using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Payments.Dtos;
using PaymentGateway.Api.Application.Payments.GetPayment;
using PaymentGateway.Api.Application.Payments.ProcessPayment;
using PaymentGateway.Api.Filters;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Extensions;

using Polly;

using Serilog;
using PaymentGateway.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((_, options) => options.Limits.MaxRequestBodySize = 1 * 1024 * 1024);

Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add<ValidateIdempotencyKeyFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = JsonDefaults.Options.PropertyNameCaseInsensitive;
        foreach (var converter in JsonDefaults.Options.Converters)
        {
            options.JsonSerializerOptions.Converters.Add(converter);
        }
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new ErrorResponse(
            "invalid_request",
            "The request body is invalid.",
            errors));
    };
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Gateway API",
        Version = "v1",
        Description = "Processes and retrieves card payments."
    });
    options.OperationFilter<IdempotencyHeaderOperationFilter>();

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

builder.Services.Configure<AcquiringBankOptions>(builder.Configuration.GetSection(AcquiringBankOptions.SectionName));
var acquiringBankOptionsConfig = builder.Configuration.GetSection(AcquiringBankOptions.SectionName).Get<AcquiringBankOptions>() ?? new AcquiringBankOptions();
builder.Services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>(client =>
{
    client.BaseAddress = new Uri(acquiringBankOptionsConfig.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(acquiringBankOptionsConfig.TimeoutSeconds);
}).AddResilienceHandler("acquiring-bank-client-pipeline", builder =>
{
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = acquiringBankOptionsConfig.MaxRetryAttempts,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = (args) => ValueTask.FromResult(args.Outcome.Exception is not null &&
            (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.InternalServerError))
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Merchants", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["https://localhost:3000"])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.Configure<PaymentsCacheOptions>(builder.Configuration.GetSection(PaymentsCacheOptions.SectionName));
var cacheConfig = builder.Configuration.GetSection(PaymentsCacheOptions.SectionName).Get<PaymentsCacheOptions>() ?? new PaymentsCacheOptions();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("PaymentsCache", policy =>
    {
        policy.Expire(TimeSpan.FromSeconds(cacheConfig.DurationSeconds));
        if (cacheConfig.VaryByQuery?.Length > 0)
        {
            policy.SetVaryByQuery(cacheConfig.VaryByQuery);
        }
        policy.Tag(cacheConfig.Tag);
    });
});

builder.Services.Configure<PaymentsRateLimitOptions>(builder.Configuration.GetSection(PaymentsRateLimitOptions.SectionName));
var rateLimitConfig = builder.Configuration.GetSection(PaymentsRateLimitOptions.SectionName).Get<PaymentsRateLimitOptions>() ?? new PaymentsRateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("PaymentsRateLimit", limiter =>
    {
        limiter.PermitLimit = rateLimitConfig.PermitLimit;
        limiter.Window = TimeSpan.FromSeconds(rateLimitConfig.WindowSeconds);
        limiter.QueueLimit = rateLimitConfig.QueueLimit;

        if (Enum.TryParse<QueueProcessingOrder>(rateLimitConfig.QueueProcessing, ignoreCase: true, out var qpo))
        {
            limiter.QueueProcessingOrder = qpo;
        }
        else
        {
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }
    });
});

builder.Services.AddCustomHealthChecks();

builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
builder.Services.AddScoped<IPaymentRequestValidator, PaymentRequestValidator>();
builder.Services.AddScoped<ICommandHandler<ProcessPaymentCommand, PaymentOperationResult>, ProcessPaymentHandler>();
builder.Services.AddScoped<IQueryHandler<GetPaymentQuery, PaymentOperationResult>, GetPaymentHandler>();
builder.Services.AddScoped<ProcessPaymentHandler>();
builder.Services.AddScoped<GetPaymentHandler>();
builder.Services.AddScoped<IETagService, ETagService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Gateway API v1");
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

app.Run();

public partial class Program { }
