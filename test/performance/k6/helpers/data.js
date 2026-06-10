// tests/performance/k6/helpers/data.js

// Test card numbers that your bank simulator accepts
const TEST_CARDS = [
    '2222405343248877',  // Mastercard - Authorized
    '2222405343248112',  // Mastercard - Declined
    '4012888888881883',  // Visa
];

const CURRENCIES = ['GBP', 'USD', 'EUR'];

export function generateUUID() {
    return crypto.randomUUID();
}

export function randomPaymentPayload(merchantId) {
    const card = TEST_CARDS[Math.floor(Math.random() * TEST_CARDS.length)];
    const currency = CURRENCIES[Math.floor(Math.random() * CURRENCIES.length)];

    return {
        merchantId: merchantId,
        cardNumber: card,
        expiryMonth: 12,
        expiryYear: 2026,
        currency: currency,
        amount: Math.floor(Math.random() * 10000) + 100,  // 1.00 to 101.00
        cvv: '456',
    };
}

export function fixedPaymentPayload(merchantId) {
    return {
        merchantId: merchantId,
        cardNumber: '2222405343248877',
        expiryMonth: 12,
        expiryYear: 2026,
        currency: 'GBP',
        amount: 9999,
        cvv: '456',
    };
}