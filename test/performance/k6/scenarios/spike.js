import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';
import { commonThresholds, checks, MERCHANT_ID } from '../config/options.js';
import { postPayment } from '../helpers/http.js';
import { fixedPaymentPayload, generateUUID } from '../helpers/data.js';

const errorRate = new Rate('payment_error_rate');

export const options = {
    insecureSkipTLSVerify: true, 
    stages: [
        { duration: '10s', target: 5 },  // normal baseline
        { duration: '30s', target: 200 },  // massive spike
        { duration: '1m', target: 200 },  // hold the spike
        { duration: '10s', target: 5 },  // drop back
        { duration: '30s', target: 5 },  // check recovery
    ],
    thresholds: {
        http_req_failed: ['rate<0.10'],   // allow more errors during spike
        http_req_duration: ['p(95)<3000'],
        payment_error_rate: ['rate<0.10'],
    },
};

export default function () {
    const idempotencyKey = generateUUID();
    const payload = fixedPaymentPayload(MERCHANT_ID);
    const res = postPayment(payload, idempotencyKey);
    errorRate.add(!check(res, checks.postPayment));
    sleep(0.2);
}