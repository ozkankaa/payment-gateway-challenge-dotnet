# Multi-stage Dockerfile for PaymentGateway.Api (.NET 8)
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore first to leverage layer caching
COPY ["PaymentGateway.Api/PaymentGateway.Api.csproj", "PaymentGateway.Api/"]
RUN dotnet restore "PaymentGateway.Api/PaymentGateway.Api.csproj"

# Copy the rest of the solution and publish
COPY . .
WORKDIR /src/PaymentGateway.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Listen on port 80 (map externally as needed)
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Copy published output from build stage
COPY --from=build /app/publish .

# Run the API
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]