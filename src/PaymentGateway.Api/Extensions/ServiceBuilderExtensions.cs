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
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.AcquiringBank;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Fraud;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.Idempotency;
using PaymentGateway.Api.Application.Features.Payments.ProcessPayment.Handlers.PaymentValidation;
using PaymentGateway.Api.Domain.Events.PaymentCaptured;
using PaymentGateway.Api.Filters;
using PaymentGateway.Api.Infrastructure;
using PaymentGateway.Api.Infrastructure.Messaging.Abstraction;
using PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Consuming;
using PaymentGateway.Api.Infrastructure.Messaging.RabbitMQ.Publishing;
using PaymentGateway.Api.Infrastructure.Outbox;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Infrastructure.Persistence.Repositories;
using PaymentGateway.Api.Infrastructure.Services.ETagService;
using PaymentGateway.Api.Infrastructure.Services.IdempotencyService;
using PaymentGateway.Api.Middleware;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Saga.Event;
using PaymentGateway.Api.Saga.Event.Consumers;
using PaymentGateway.Api.Saga.Request;
using PaymentGateway.Api.Saga.Request.Handlers;

namespace PaymentGateway.Api.Extensions;

public static class ServiceBuilderExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddControllers();
        services.AddProblemDetails();

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
        services.AddCustomHealthChecks();

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

            options.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Payment Gateway API - Saga - Async",
                Version = "v2",
                Description = "Processes and retrieves card payments."
            });

            options.SwaggerDoc("v3", new OpenApiInfo
            {
                Title = "Payment Gateway API - Saga - Sync",
                Version = "v3",
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

        services.AddScoped<ICommandHandler<ProcessPaymentCommand, IDictionary<string, string[]>>, PaymentValidationHandler>();
        services.AddScoped<IPaymentValidationHandler, PaymentValidationHandler>();
        services.AddScoped<PaymentValidationHandler>();

        services.AddScoped<ICommandHandler<IdempotencyCheckCommand, IdempotencyResult>, IdempotencyCheckHandler>();
        services.AddScoped<IIdempotencyCheckHandler, IdempotencyCheckHandler>();
        services.AddScoped<IdempotencyCheckHandler>();

        services.AddScoped<ICommandHandler<IdempotencyUpdateCommand, IdempotencyResult>, IdempotencyUpdateHandler>();
        services.AddScoped<IIdempotencyUpdateHandler, IdempotencyUpdateHandler>();
        services.AddScoped<IdempotencyUpdateHandler>();

        services.AddScoped<ICommandHandler<FraudCheckCommand, FraudCheckResult>, FraudCheckHandler>();
        services.AddScoped<IFraudCheckHandler, FraudCheckHandler>();
        services.AddScoped<FraudCheckHandler>();

        services.AddScoped<ICommandHandler<AcquiringBankAuthorizeCommand, AcquiringBankAuthorizeResult>, AcquiringBankAuthorizeHandler>();
        services.AddScoped<IAcquiringBankAuthorizeHandler, AcquiringBankAuthorizeHandler>();
        services.AddScoped<AcquiringBankAuthorizeHandler>();

        services.AddHttplients(configuration);

        services.AddOutboxEventPublisher(configuration);

        services.AddPaymentDbContext(configuration);

        services.AddSaga(configuration);

        return services;
    }

    public static IServiceCollection AddSaga(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentEventDbContext>(options => options.UseSqlite(
                configuration.GetConnectionString("PaymentSagaDb"),
                x => x.MigrationsHistoryTable("__EFMigrationsHistory_PaymentSaga")));

        var rabbitOptions = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddSagaStateMachine<PaymentRequestStateMachine, PaymentRequestState>()
            .InMemoryRepository();

            x.AddSagaStateMachine<PaymentEventStateMachine, PaymentEventState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Optimistic;

                r.AddDbContext<DbContext, PaymentEventDbContext>((provider, options) => options.UseSqlite(configuration.GetConnectionString("PaymentSagaDb")));

                r.UseSqlite();
            });

            x.AddConsumer<ValidatePaymentEventConsumer>();
            x.AddConsumer<CheckIdempotencyEventConsumer>();
            x.AddConsumer<CheckFraudEventConsumer>();
            x.AddConsumer<AuthorizePaymentEventConsumer>();
            x.AddConsumer<CapturePaymentEventConsumer>();

            x.AddConsumer<ValidatePaymentRequestHandler>();
            x.AddConsumer<CheckIdempotencyRequestHandler>();
            x.AddConsumer<CheckFraudRequestHandler>();
            x.AddConsumer<AuthorizePaymentRequestHandler>();
            x.AddConsumer<CapturePaymentRequestHandler>();

            //x.AddEntityFrameworkOutbox<PaymentSagaDbContext>(o =>
            //{
            //    //o.QueryDelay = TimeSpan.FromSeconds(1);

            //    o.UseSqlite();

            //    o.UseBusOutbox();
            //});

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitOptions.HostName, "/", h =>
                {
                    h.Username(rabbitOptions.UserName);
                    h.Password(rabbitOptions.Password);
                });

                cfg.ReceiveEndpoint("payment-event", e => e.ConfigureSaga<PaymentEventState>(context));
                cfg.ReceiveEndpoint("payment-request", e => e.ConfigureSaga<PaymentRequestState>(context));

                // SQLite is not ideal for pessimistic locking (unblock this when use Postgres or SQL Server), but we can still use message retry for transient exceptions like deadlocks or connection issues.
                cfg.UseMessageRetry(r => r.Exponential(
                        retryLimit: 5,
                        minInterval: TimeSpan.FromSeconds(1),
                        maxInterval: TimeSpan.FromSeconds(30),
                        intervalDelta: TimeSpan.FromSeconds(5)));

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    public static IServiceCollection AddPaymentDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(
                configuration.GetConnectionString("PaymentDb"),
                x => x.MigrationsHistoryTable("__EFMigrationsHistory_Payment")));

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
