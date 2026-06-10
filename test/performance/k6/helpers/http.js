import http from 'k6/http';
import { BASE_URL } from '../config/options.js';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

export function postPayment(payload, idempotencyKey = null) {
    const headers = { ...JSON_HEADERS };

    // Your API supports an optional Idempotency-Key header
    if (idempotencyKey) {
        headers['Idempotency-Key'] = idempotencyKey;
    }

    return http.post(
        `${BASE_URL}/api/v1/payments`,
        JSON.stringify(payload),
        { headers }
    );
}

export function getPayment(id) {
    return http.get(
        `${BASE_URL}/api/v1/payments/${id}`,
        { headers: JSON_HEADERS }
    );
}