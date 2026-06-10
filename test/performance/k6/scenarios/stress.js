import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';
import { commonThresholds, checks, MERCHANT_ID } from '../config/options.js';
import { postPayment } from '../helpers/http.js';
import { randomPaymentPayload, generateUUID } from '../helpers/data.js';

const paymentDuration = new Trend('payment_post_duration');
const errorRate = new Rate('payment_error_rate');

export const options = {
    insecureSkipTLSVerify: true, 
    stages: [
        { duration: '1m', target: 50 },   // ramp to normal
        { duration: '1m', target: 100 },   // push beyond normal
        { duration: '1m', target: 200 },   // stress
        { duration: '1m', target: 300 },   // find the breaking point
        { duration: '1m', target: 0 },   // ramp down and recover
    ],
    thresholds: {
        ...commonThresholds,
        http_req_duration: ['p(95)<2000'],  // relaxed threshold for stress
        payment_error_rate: ['rate<0.05'],  // allow up to 5% errors under stress
    },
};

export default function () {
    const idempotencyKey = generateUUID();
    const payload = randomPaymentPayload(MERCHANT_ID);
    const res = postPayment(payload, idempotencyKey);
    paymentDuration.add(res.timings.duration);
    errorRate.add(!check(res, checks.postPayment));
    sleep(0.5);
}