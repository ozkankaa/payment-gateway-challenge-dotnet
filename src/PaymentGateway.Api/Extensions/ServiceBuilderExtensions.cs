using System.Reflection;
using System.Threading.RateLimiting;

using Asp.Versioning;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

using PaymentGateway.Api.Application.Abstractions.CQRS;
using PaymentGateway.Api.Application.Abstractions.Persistence;
using PaymentGateway.Api.Application.Features.Payments.DomainEvents;
using PaymentGateway.Api.Application.Features.Payments.DomainEvents.PaymentCaptured;
using PaymentGateway.Api.Application.Features.Payments.Dtos;
using PaymentGateway.Api.Application.Features.Payments.GetPayment;
using PaymentGateway.Api.Application.Features.Payments.IntegrationEvents;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment;
using PaymentGateway.Api.Domain.Events.PaymentCaptured;
using PaymentGateway.Api.Filters;
using PaymentGateway.Api.Grpc;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Infrastructure.BackgroundServices;
using PaymentGateway.Api.Infrastructure.Messaging.Abstraction;
using PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Consuming;
using PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Publishing;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Infrastructure.Persistence.Repositories;
using PaymentGateway.Api.Infrastructure.Services.ETagService;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;
using PaymentGateway.Api.Middleware;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Extensions;

public static class ServiceBuilderExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddControllers();
        services.AddGrpc();
        services.AddGrpcReflection();

        services.ConfigureServices(configuration);
        services.AddApiVersioning();

        services.AddHttpContextAccessor();
        services.AddTransient<CorrelationIdHandler>();

        services.AddEndpointsApiExplorer();
        services.AddSwagger();

        services.AddCors(configuration);
        services.AddOutputCache(configuration);
        services.AddRateLimit(configuration);
        services.AddCustomHealthChecks(configuration);

        services.AddApplicationOpenTelemetry(configuration);

        services.AddOtherDependencies(configuration);

        return services;
    }

    public static IServiceCollection AddControllers(this IServiceCollection services)
    {
        services.AddControllers(options => options.Filters.Add<ValidateIdempotencyKeyFilter>())
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = JsonDefaults.Options.PropertyNameCaseInsensitive;
            foreach (var converter in JsonDefaults.Options.Converters)
            {
                options.JsonSerializerOptions.Converters.Add(converter);
            }
        });
        return services;
    }

    public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceOptions>(configuration.GetSection(ServiceOptions.SectionName));

        var serviceOptions = configuration
            .GetSection("Service")
            .Get<ServiceOptions>()
            ?? new ServiceOptions();

        services.AddSingleton(serviceOptions);

        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
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
        });
        return services;
    }

    public static IServiceCollection AddApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
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
        return services;
    }

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
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
        return services;
    }

    public static IServiceCollection AddCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options => options.AddPolicy("Merchants", policy => policy
        .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["https://localhost:3000"])
        .AllowAnyHeader()
        .AllowAnyMethod()));
        return services;
    }

    public static IServiceCollection AddOutputCache(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceOptions = configuration
            .GetSection("Service")
            .Get<ServiceOptions>()
            ?? new ServiceOptions();

        services.AddOutputCache(options => options.AddPolicy("payments", policy =>
        {
            policy.Expire(TimeSpan.FromSeconds(serviceOptions.OutputCache.DurationSeconds));
            if (serviceOptions.OutputCache.VaryByQuery?.Length > 0)
            {
                policy.SetVaryByQuery(serviceOptions.OutputCache.VaryByQuery);
            }
            policy.Tag(serviceOptions.OutputCache.Tag);
        }));
        return services;
    }

    public static IServiceCollection AddRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceOptions = configuration
            .GetSection("Service")
            .Get<ServiceOptions>()
            ?? new ServiceOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("PaymentsRateLimit", limiter =>
            {
                limiter.PermitLimit = serviceOptions.RateLimit.PermitLimit;
                limiter.Window = TimeSpan.FromSeconds(serviceOptions.RateLimit.WindowSeconds);
                limiter.QueueLimit = serviceOptions.RateLimit.QueueLimit;

                limiter.QueueProcessingOrder = Enum.TryParse<QueueProcessingOrder>(serviceOptions.RateLimit.QueueProcessing, ignoreCase: true, out var qpo)
                    ? qpo
                    : QueueProcessingOrder.OldestFirst;
            });
        });
        return services;
    }

    public static IServiceCollection AddHttplients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAcquiringBankClient(configuration);
        services.AddFraudServiceClient(configuration);
        return services;
    }

    public static IServiceCollection AddOtherDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
        services.AddScoped<IProcessPaymentCommandValidator, ProcessPaymentCommandValidator>();
        services.AddScoped<ICommandHandler<ProcessPaymentCommand, PaymentOperationResultDto>, ProcessPaymentHandler>();
        services.AddScoped<IQueryHandler<GetPaymentQuery, PaymentOperationResultDto>, GetPaymentHandler>();
        services.AddScoped<ProcessPaymentHandler>();
        services.AddScoped<GetPaymentHandler>();
        services.AddScoped<IETagService, ETagService>();
        services.AddSingleton<IIdempotencyService, IdempotencyService>();

        services.AddScoped<IDomainEventHandler, DomainEventHandler>();
        services.AddScoped<ICommandHandler<PaymentCapturedDomainEvent, bool>, PaymentCapturedDomainEventHandler>();
        services.AddScoped<PaymentCapturedDomainEventHandler>();

        services.AddScoped<ICommandHandler<IntegrationEvent>, IntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler, IntegrationEventHandler>();
        services.AddScoped<IntegrationEventHandler>();


        services.AddHttplients(configuration);

        services.AddOutboxEventPublisher(configuration);

        services.AddPaymentDbContext(configuration);

        return services;
    }    

    public static IServiceCollection AddPaymentDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options =>
        {
            options.UseSqlite(
                configuration.GetConnectionString("PaymentDb"));
        });
        return services;
    }

    public static IServiceCollection AddOutboxEventPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();
        services.AddHostedService<OutboxMessageProcessor>();
        services.AddHostedService<IntegrationEventConsumer>();

        return services;
    }
}
