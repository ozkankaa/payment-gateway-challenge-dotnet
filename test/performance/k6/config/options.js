// tests/performance/k6/config/options.js

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const MERCHANT_ID = __ENV.MERCHANT_ID || '3fa85f64-5717-4562-b3fc-2c963f66afa6';

export const commonThresholds = {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
};

export const options = {
    insecureSkipTLSVerify: true,    // ← add this
};

export const checks = {
    postPayment: {
        'POST /payments - status 201 or 200': (r) => r.status === 201 || r.status === 200,
        'POST /payments - has id': (r) => r.body && r.json('id') !== undefined,
        'POST /payments - has status': (r) => r.body && r.json('status') !== undefined,
        'POST /payments - response < 1s': (r) => r.timings.duration < 1000,
    },
    getPayment: {
        'GET /payments/:id - status 200': (r) => r.status === 200,
        'GET /payments/:id - has id': (r) => r.body && r.json('id') !== undefined,
        'GET /payments/:id - response < 500ms': (r) => r.timings.duration < 500,
    },
};