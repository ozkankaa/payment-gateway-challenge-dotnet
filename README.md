# PaymentGateway.Api

A simple payment gateway API implemented in .NET 8 (C# 12). This project exposes endpoints for creating and retrieving payments with idempotency, ETag support, output caching, and rate limiting.

## Key features

- Built with .NET 8 and C# 12
- Stateless REST API with versioning (v1.0)
- Endpoints:
  - `GET /api/v1/payments/{id}` — retrieve a payment
  - `POST /api/v1/payments` — create/process a payment
- Idempotency support via the `Idempotency-Key` header
- ETag generation and conditional GETs
- Output caching and cache eviction by tag: `PaymentsCache`
- Rate limiting policy: `PaymentsRateLimit`
- Retry only when the failure may be temporary
- Circuit breaker : Stops calling a failing dependency temporarily
- Bulkhead isolation : Limits how many concurrent calls can go to one dependency
- Structured logging with using Serilog for better observability
- Unit and integration tests included to ensure code quality
- Health checks for application liveness and readiness
- OpenTelemtry implementation for better observability and added custom metrics like success/failure payments
- SAGA pattern implementation via handling payment
- CQRS pattern implementation handling business logic in application handlers
- DDD implementation with aggragated payment domain and handling domain events
- EDD implementation with using RabbitMQ sending messages to external services
- Using Sqlite database for storing payments and outbox events via EntityFrameworkCore and UnitOfWork implementation and RowVersioning for concurrency
- Added Azure/AWS/K8S deployment files for Github Actions

## Prerequisites

Before you begin, ensure you have the following installed:

- .NET 8 SDK
- Visual Studio 2022 or later (recommended) or VS Code
- (Optional) Docker for containerized runs

## Build

To build the project, run the following command in your terminal:


dotnet build


## Run

To run the API, navigate to the solution root and execute:


dotnet run --project PaymentGateway.Api


The API will be available at `https://localhost:{port}/api/v1/payments`.

## Configuration

Configuration settings are located in `appsettings.json` and its environment-specific variants. Important settings include:

- Acquiring bank endpoint URL (for the bank simulator)
- Fraud service endpoint URL (for the fraud simulator)
- RabbitMQ settings (for the publish/consume integration messages)
- Database connection string
- Logging configuration
- Rate limiting policy name: `PaymentsRateLimit`
- Output cache settings and tag-based eviction: `PaymentsCache`
- OpenTelemtry settings

Feel free to adjust ports, logging levels, and provider settings as needed.

### Running the external dependencies

From the repository root, start the simulator with Docker Compose:

`docker compose -f docker-compose.integration.yml up -d`

## Bank simulator (external dependency)

This project depends on a provided bank simulator that must be running when executing the API locally (or in an integration test environment). The simulator is defined in the repository's Docker Compose file (`docker-compose.yml` / `docker-compose.ml`) and exposes a simple HTTP endpoint that mimics a payment processor.

### How the simulator behaves

- If any required field is missing in the request, the simulator returns `400 Bad Request` with an error message.
- If all required fields are present, the simulator's response depends on the last digit of the card number:
  - Card number ends with an odd digit (1, 3, 5, 7, 9): returns `200 OK` with an authorized response and a generated `authorization_code`.
  - Card number ends with an even digit (2, 4, 6, 8): returns `200 OK` with an unauthorized response.
  - Card number ends with `0`: returns `503 Service Unavailable` (simulates a downstream service failure).


### Testing against the simulator

- **Authorized example (odd last digit except 1):**

```
{
  "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cardNumber": "4111111111111113",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 10.00
}
```

- **Unauthorized example (even last digit):**

```
{
  "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cardNumber": "4111111111111112",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 10.00
}
```

- **Acquired bank error example (ends with 0):**

```
{
  "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cardNumber": "4111111111111110",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 10.00
}
```

### Notes

- Ensure the simulator service is reachable from the API (check network and ports in the compose file).
- When running integration tests or debugging locally, start the bank simulator first so the API's payment processing can call the simulator endpoint.


## Fraud simulator (external dependency)

This project depends on a provided fraud simulator that must be running when executing the API locally (or in an integration test environment). The simulator is defined in the repository's Docker Compose file (`docker-compose.yml` / `docker-compose.ml`) and exposes a simple HTTP endpoint that mimics a fraud processor.

### How the fraud simulator behaves

- If any required field is missing in the request, the simulator returns `400 Bad Request` with an error message.
- If all required fields are present, the simulator's response depends on the last digit of the card number:
  - Card number ends with all digit `except 1`: returns `200 OK` with an authorized response and a generated `authorization_code`.
  - Card number ends with `1`: returns `200 OK` with an unauthorized response.
  - Card number ends with `9`: returns `503 Service Unavailable` (simulates a downstream service failure).


### Testing against the fraud simulator

- **Authorized example (all digit except 1):**

```
{
  "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cardNumber": "4111111111111113",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 10.00
}
```

- **Unauthorized example (1):**

```
{
  "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cardNumber": "4111111111111111",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 10.00
}
```

- **Fraud service error example (ends with 9):**

```
{
  "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cardNumber": "4111111111111119",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 10.00
}
```

### Notes

- Ensure the simulator service is reachable from the API (check network and ports in the compose file).
- When running integration tests or debugging locally, start the bank simulator first so the API's payment processing can call the simulator endpoint.


## Health checks

This API exposes both liveness and readiness probes and includes an external readiness check for the acquiring bank (bank simulator).

- Endpoints:
  - `GET /health/live` — liveness probe. Should return 200 if the application is running.
  - `GET /health/ready` — readiness probe. Includes registered readiness checks such as the acquiring bank health check.

### How to test

1. Start the external integrarion dependencies: `docker compose -f docker-compose.integration.yml up -d`.
2. Start the API (`dotnet run --project PaymentGateway.Api`).
3. Call the endpoints:
   - `GET /health/live` — should return 200 when the app is running.
   - `GET /health/ready` — should return 200 when all externals are reachable and responding.
4. Simulate failures:
   - Use a probe card number ending with `0` in the bank simulator to trigger 503 and observe the readiness endpoint return Unhealthy.
   - Use a probe card number ending with `9` in the fraud simulator to trigger 503 and observe the readiness endpoint return Unhealthy.

## API

### POST /api/v1/payments

**Headers:**
- `Content-Type: application/json`
- Optional: `Idempotency-Key: <key>` — ensures safe retries

**Request body (example):**

```
{
  "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cardNumber": "4111111111111111",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 100.00
}
```

**Possible responses:**
- `201 Created` — payment created. Location header points to `GET /api/v1/payments/{id}`
- `200 OK` — duplicate idempotent request returning existing resource
- `400 Bad Request` — validation errors
- `409 Conflict` — conflict detected
- `503 Service Unavailable` — downstream service unavailable

### GET /api/v1/payments/{id}

**Headers:**
- Optional: `If-None-Match: "<etag>"` — server returns `304 Not Modified` when appropriate

**Responses:**
- `200 OK` — returns payment resource
- `304 Not Modified` — resource not modified according to ETag
- `404 Not Found` — no payment with the requested id

## Testing

To run unit tests, use the following command:


dotnet test


Test projects include validators and handler tests. You can also use test coverage and CI tools as desired to maintain code quality.

## Deployment

### Github Actions

#### Azure

GitHub secret needed `AZURE_WEBAPP_PUBLISH_PROFILE`

Add sensitive values manually in Azure Configuration or Key Vault references:
```
ConnectionStrings__PaymentDb
RabbitMq__HostName
RabbitMq__UserName
RabbitMq__Password
RabbitMq__Port
RabbitMq__VirtualHost
AcquiringBank__BaseUrl
FraudService__BaseUrl
Service__OpenTelemetry__OtlpEndpoint
Serilog__WriteTo__1__Args__endpoint
```

### AWS

GitHub secrets needed for  AWS_GITHUB_ACTIONS_ROLE_ARN

```
AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY
```

Store AWS SSM parameters

```
aws ssm put-parameter \
  --name "/payment-gateway/prod/payment-db" \
  --type "SecureString" \
  --value "your-db-connection-string"

aws ssm put-parameter \
  --name "/payment-gateway/prod/rabbitmq-host" \
  --type "SecureString" \
  --value "your-rabbitmq-host"

aws ssm put-parameter \
  --name "/payment-gateway/prod/rabbitmq-username" \
  --type "SecureString" \
  --value "your-rabbitmq-user"

aws ssm put-parameter \
  --name "/payment-gateway/prod/rabbitmq-password" \
  --type "SecureString" \
  --value "your-rabbitmq-password"

aws ssm put-parameter \
  --name "/payment-gateway/prod/acquiring-bank-base-url" \
  --type "SecureString" \
  --value "https://your-acquiring-bank"

aws ssm put-parameter \
  --name "/payment-gateway/prod/fraud-service-base-url" \
  --type "SecureString" \
  --value "https://your-fraud-service"

aws ssm put-parameter \
  --name "/payment-gateway/prod/otlp-endpoint" \
  --type "SecureString" \
  --value "http://your-otel-collector:4318"

aws ssm put-parameter \
  --name "/payment-gateway/prod/otlp-logs-endpoint" \
  --type "SecureString" \
  --value "http://your-otel-collector:4318/v1/logs"
```

### K8S

Required GitHub secret:`KUBE_CONFIG`

Store it base64 encoded: `cat ~/.kube/config | base64`

#### Apply order

```bash
kubectl apply -f k8s/prod/namespace.yaml
kubectl apply -f k8s/prod/configmap.yaml
kubectl apply -f k8s/prod/secret.yaml
kubectl apply -f k8s/prod/service.yaml
kubectl apply -f k8s/prod/deployment.yaml
kubectl apply -f k8s/prod/hpa.yaml
kubectl apply -f k8s/prod/pdb.yaml
kubectl apply -f k8s/prod/ingress.yaml
kubectl apply -f k8s/prod/istio-gateway.yaml
kubectl apply -f k8s/prod/istio-virtualservice.yaml
```

#### Replace before production

- `api.your-domain.com`
- `ghcr.io/YOUR_ORG/payment-gateway-api:latest`
- all values in `secret.yaml`
- project path in `.github/workflows/k8s-prod.yml` if your API project path differs


## Coding standards

This repository adheres to the coding rules defined in `.editorconfig` and outlines contribution expectations in `CONTRIBUTING.md`. Please follow these guidelines when making changes.

## Contributing

To contribute to this project, please follow these steps:

- Fork the repository and create a feature branch.
- Ensure that the code builds and all tests pass locally.
- Follow the formatting rules in `.editorconfig`.
- Open a pull request (PR) with a descriptive title and summary of changes.

## Troubleshooting

If you encounter issues, consider the following:

- **Rate Limiting:** If you experience rate limiting while testing, check the rate limiting settings in the configuration and any test harness that might reuse keys.
- **Caching Issues:** Remember that the cache is evicted on successful create operations by the tag `payments`.
- **Health Checks:** Ensure the bank simulator is running for readiness checks. If readiness returns Unhealthy, confirm the simulator endpoint and network settings.

## License

Specify your project's license here.

This revised README now includes comprehensive information about the health checks, ensuring that users understand their role and how to interact with them while maintaining the overall structure and clarity of the document.