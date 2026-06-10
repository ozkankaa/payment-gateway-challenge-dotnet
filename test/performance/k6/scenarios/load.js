import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';
import { commonThresholds, checks, MERCHANT_ID } from '../config/options.js';
import { postPayment, getPayment } from '../helpers/http.js';
import { randomPaymentPayload, generateUUID } from '../helpers/data.js';

const paymentDuration = new Trend('payment_post_duration');
const errorRate = new Rate('payment_error_rate');

export const options = {
    insecureSkipTLSVerify: true, 
    stages: [
        { duration: '1m', target: 20 },   // ramp up to 20 users
        { duration: '3m', target: 20 },   // hold at 20 users
        { duration: '30s', target: 0 },   // ramp down
    ],
    thresholds: {
        ...commonThresholds,
        payment_post_duration: ['p(95)<800'],
        payment_error_rate: ['rate<0.01'],
    },
};

export default function () {
    const idempotencyKey = generateUUID();
    const payload = randomPaymentPayload(MERCHANT_ID);
    const postRes = postPayment(payload, idempotencyKey);
    paymentDuration.add(postRes.timings.duration);

    const ok = check(postRes, checks.postPayment);
    errorRate.add(!ok);

    if (postRes.status === 201 || postRes.status === 200) {
        sleep(0.5);
        const id = postRes.json('id');
        const getRes = getPayment(id);
        check(getRes, checks.getPayment);
    }

    sleep(1);
}