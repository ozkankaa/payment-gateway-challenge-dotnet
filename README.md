# PaymentGateway.Api

A simple payment gateway API implemented in .NET 8 (C# 12). This project exposes endpoints for creating and retrieving payments with idempotency, ETag support, output caching, and rate limiting.

## Key features

- Built with .NET 8 and C# 12
- REST API with versioning (v1.0)
- Endpoints:
  - `GET /api/v1/payments/{id}` — retrieve a payment
  - `POST /api/v1/payments` — create/process a payment
- Idempotency support via the `Idempotency-Key` header
- ETag generation and conditional GETs
- Output caching and cache eviction by tag: `PaymentsCache`
- Rate limiting policy: `PaymentsRateLimit`
- Structured logging for better observability
- Unit tests included to ensure code quality
- Health checks for application liveness and readiness

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
- Logging configuration
- Rate limiting policy name: `PaymentsRateLimit`
- Output cache settings and tag-based eviction: `PaymentsCache`

Feel free to adjust ports, logging levels, and provider settings as needed.

## Bank simulator (external dependency)

This project depends on a provided bank simulator that must be running when executing the API locally (or in an integration test environment). The simulator is defined in the repository's Docker Compose file (`docker-compose.yml` / `docker-compose.ml`) and exposes a simple HTTP endpoint that mimics a payment processor.

### How the simulator behaves

- If any required field is missing in the request, the simulator returns `400 Bad Request` with an error message.
- If all required fields are present, the simulator's response depends on the last digit of the card number:
  - Card number ends with an odd digit (1, 3, 5, 7, 9): returns `200 OK` with an authorized response and a generated `authorization_code`.
  - Card number ends with an even digit (2, 4, 6, 8): returns `200 OK` with an unauthorized response.
  - Card number ends with `0`: returns `503 Service Unavailable` (simulates a downstream service failure).

### Running the simulator

From the repository root, start the simulator with Docker Compose:


docker-compose up bank_simulator


(or `docker-compose up` to run all services defined in the compose file).

### Testing against the simulator

- **Authorized example (odd last digit):**

```
{
  "cardNumber": "4111111111111111",
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
  "cardNumber": "4111111111111112",
  "expiryMonth": 12,
  "expiryYear": 2026,
  "cvv": "123",
  "currency": "USD",
  "amount": 10.00
}
```

- **Downstream error example (ends with 0):**

```
{
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

## Health checks

This API exposes both liveness and readiness probes and includes an external readiness check for the acquiring bank (bank simulator).

- Endpoints:
  - `GET /health/live` — liveness probe. Should return 200 if the application is running.
  - `GET /health/ready` — readiness probe. Includes registered readiness checks such as the acquiring bank health check.

### Acquiring bank health check

- Implemented by `AcquiringBankHealthCheck`
- The check uses the registered `IAcquiringBankClient` to send a small probe `BankPaymentRequest` to the acquiring bank endpoint. The probe uses a card number ending with a digit that forces a reachable response (by default the implementation uses a card ending with `2` to force a 200 response from the simulator while representing an "unauthorized" outcome).
- Outcomes handled:
  - HealthCheck returns Healthy (200) when the client receives a non-null response from the simulator.
  - Returns Degraded when the client returns null (reachable but no content).
  - Returns Unhealthy when the simulator responds with `503 Service Unavailable` or when the request throws an exception indicating the service is unavailable.

### How to test

1. Start the bank simulator: `docker-compose up bank_simulator`.
2. Start the API (`dotnet run --project PaymentGateway.Api`).
3. Call the endpoints:
   - `GET /health/live` — should return 200 when the app is running.
   - `GET /health/ready` — should return 200 when the bank simulator is reachable and responding.
4. Simulate failures:
   - Use a probe card number ending with `0` in the simulator to trigger 503 and observe the readiness endpoint return Unhealthy.

## API

### POST /api/v1/payments

**Headers:**
- `Content-Type: application/json`
- Optional: `Idempotency-Key: <key>` — ensures safe retries

**Request body (example):**

```
{
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