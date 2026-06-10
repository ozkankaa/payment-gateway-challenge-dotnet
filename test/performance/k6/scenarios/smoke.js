import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';
import { commonThresholds, checks, MERCHANT_ID } from '../config/options.js';
import { postPayment, getPayment } from '../helpers/http.js';
import { fixedPaymentPayload, generateUUID } from '../helpers/data.js';

const paymentDuration = new Trend('payment_post_duration');
const getPaymentDuration = new Trend('payment_get_duration');
const errorRate = new Rate('payment_error_rate');

export const options = {
    insecureSkipTLSVerify: true, 
    vus: 1,
    duration: '30s',
    thresholds: {
        ...commonThresholds,
        payment_error_rate: ['rate<0.05'],
    },
};

export default function () {
    // 1. POST a payment
    const idempotencyKey = generateUUID();
    const payload = fixedPaymentPayload(MERCHANT_ID);
    const postRes = postPayment(payload, idempotencyKey);
    paymentDuration.add(postRes.timings.duration);

    const postOk = check(postRes, checks.postPayment);
    errorRate.add(!postOk);

    // 2. GET the payment back (if created)
    if (postRes.status === 201 || postRes.status === 200) {
        const id = postRes.json('id');
        const getRes = getPayment(id);
        getPaymentDuration.add(getRes.timings.duration);
        check(getRes, checks.getPayment);
    }

    sleep(1);
}