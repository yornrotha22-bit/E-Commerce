# Payment integration

## Current state

| Method | Provider code | Behaviour |
|---|---|---|
| Cash on delivery | `COD` | Authorized staff confirm cash collected by the courier. |
| Bakong KHQR | `BAKONG` | Creates an amount-bound dynamic KHQR, checks the transaction through Bakong Open API, and marks it paid only after amount, currency, and receiving account match. |

Customers see the QR on their order page. The page checks payment status while it remains open. A QR is valid for 1–10 minutes (5 minutes by default); reloading the order page after expiry creates a new QR.

## Bakong setup

1. Use a Bakong account registered with a commercial bank. Register the project at the [Bakong Open API portal](https://api-bakong.nbc.gov.kh/) (or the SIT portal below) and obtain an API token. NBC's Open API guide says tokens expire after 90 days, so renew and replace them before expiry.
2. Set the merchant account ID and display name, then enable online payment. `MerchantName` must be at most 25 characters; `MerchantCity` must be at most 15. The store currency must be `USD` or `KHR`.
3. Put the token in a secret store or environment variable. Do not commit it in `appsettings.json`.
4. Apply database migration `20261025090000_AddBakongPaymentQrFields` before running the updated app.

Production environment variables:

```text
Payments__Online__Enabled=true
Payments__Bakong__ApiBaseUrl=https://api-bakong.nbc.gov.kh
Payments__Bakong__ApiToken=<secret token>
Payments__Bakong__AccountId=<merchant Bakong account ID>
Payments__Bakong__MerchantName=<merchant display name>
Payments__Bakong__MerchantCity=Phnom Penh
Payments__Bakong__AcquiringBank=<optional acquiring bank>
Payments__Bakong__ExpiryMinutes=5
```

For SIT, use `https://sit-api-bakong.nbc.org.kh` as `Payments__Bakong__ApiBaseUrl` and a SIT token/account. Do not use a production token against SIT or a SIT token in production.

The official NBC KHQR SDK generates the QR payload locally. The server sends its MD5 to Bakong's `check_transaction_by_md5` endpoint with the Bearer token. Only a successful API response whose transaction hash is unused and whose amount, currency, and receiving account match the order marks the payment paid. A customer redirect or browser claim is never treated as payment proof.

## Architecture

- `BakongPaymentProvider` uses the NBC C# KHQR SDK and `QRCoder` to produce the dynamic QR image.
- The QR payload, MD5, and expiry are stored on the payment record so refreshes keep the same QR until expiry.
- `PaymentService.CheckBakongPaymentAsync` owns server-side verification and payment state changes.
- `OrdersController.CheckBakongPayment` only checks orders owned by the signed-in customer.
- Unique `(Provider, TransactionReference)` indexing prevents replaying a Bakong transaction hash against multiple payments.

## Limitations

- The merchant must supply valid Bakong account details and an active API token before checkout offers Bakong.
- There is no refund flow yet. Refunds must be handled by staff outside the application.
