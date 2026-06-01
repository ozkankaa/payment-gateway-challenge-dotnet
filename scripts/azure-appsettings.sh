#!/usr/bin/env bash
set -euo pipefail

RESOURCE_GROUP="payment-gateway-rg"
APP_NAME="payment-gateway-api"

az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --settings \
  ASPNETCORE_ENVIRONMENT="Production" \
  Service__Name="payment-service" \
  Service__Version="1.0.0" \
  Service__Environment="production" \
  Service__OutputCache__DurationSeconds="60" \
  Service__OutputCache__Tag="payments" \
  Service__RateLimit__PermitLimit="80" \
  Service__RateLimit__WindowSeconds="60" \
  Service__RateLimit__QueueLimit="20" \
  Service__RateLimit__QueueProcessing="OldestFirst" \
  RabbitMq__ExchangeName="payment-domain-events" \
  RabbitMq__QueueName="payment-domain-events-queue" \
  RabbitMq__RoutingKey="payment.domain-event" \
  RabbitMq__PrefetchCount="10"