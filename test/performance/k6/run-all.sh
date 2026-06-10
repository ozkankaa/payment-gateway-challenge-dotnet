#!/bin/bash
# tests/performance/k6/run-all.sh

set -e  # stop on any error

BASE_URL=${BASE_URL:-"http://host.docker.internal:5000"}
INFLUX_URL="http://influxdb:8086/k6"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=== Running smoke test ==="
k6 run --out influxdb=$INFLUX_URL \
       -e BASE_URL=$BASE_URL \
       $SCRIPT_DIR/scenarios/smoke.js

echo "=== Running load test ==="
k6 run --out influxdb=$INFLUX_URL \
       -e BASE_URL=$BASE_URL \
       $SCRIPT_DIR/scenarios/load.js

echo "=== Running stress test ==="
k6 run --out influxdb=$INFLUX_URL \
       -e BASE_URL=$BASE_URL \
       $SCRIPT_DIR/scenarios/stress.js

echo "=== Running spike test ==="
k6 run --out influxdb=$INFLUX_URL \
       -e BASE_URL=$BASE_URL \
       $SCRIPT_DIR/scenarios/spike.js

echo "=== All tests complete. Open Grafana at http://localhost:3000 ==="