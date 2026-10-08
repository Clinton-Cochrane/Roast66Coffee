# In-person Card contract

Card collection uses `IInPersonPaymentGateway`, separately from the redirect-based `IPaymentGateway` checkout API. Roast66 passes the saved order total, USD currency, internal payment/order IDs, staff identity metadata, and a persisted server idempotency key to `StartPaymentAsync`. The browser and public responses contain no provider transaction references, reader configuration, or card data.

## Staff API

Both operations require the Admin role:

```http
POST /api/payments/in-person
Content-Type: application/json

{ "orderId": 123 }
```

```json
{
  "paymentId": "00000000-0000-0000-0000-000000000001",
  "orderId": 123,
  "provider": "configured-provider",
  "status": "pending",
  "amount": 7.25,
  "currency": "USD",
  "paidUtc": null
}
```

`GET /api/payments/in-person/{paymentId}` returns the same DTO from Roast66's database, with no provider request. Status is `pending`, `paid`, or `failed`; `paidUtc` is supplied only for an authoritative paid result. The browser polls this endpoint every two seconds while pending, using sequential requests. Closing the chooser does not cancel collection, start another charge, or mark the order paid. Observation continues while Orders remains mounted and stops when the payment reaches a terminal state or the workspace unmounts.

Invalid order IDs return 400, missing orders/payments return 404, and already-paid orders return 409. Missing/unconfigured providers return a useful 503. A confirmed failed attempt allows another Card click to create a new attempt; a network failure checking status leaves the existing attempt pending.

## Configuration and adapter responsibilities

`Payments:InPersonProvider` is an explicit server-side selection, independent of `Payments:DefaultProvider` for online checkout. It defaults to empty. Register a real adapter as `IInPersonPaymentGateway` and configure that provider before accepting in-person Card payments. No fake or fallback adapter is registered by production startup; tests explicitly register `FakeInPersonGateway` from the test assembly.

An adapter must honor the idempotency key and report Failed only when collection definitively failed. It may report an already-confirmed Paid result from Start; merely launching collection is Pending. Its webhook parser must verify provider authenticity and return `GatewayPaymentEvent` identified by the internal payment ID or provider payment reference. `POST /api/payments/{provider}/webhook` applies those events through the existing replay/concurrency-aware payment settlement path. If an adapter also implements online checkout for the same provider, its shared webhook parser must recognize events for both flows.

No terminal ID, Stripe SDK addition, Bluetooth, reader discovery, browser/provider polling, or hardware implementation is part of this contract.

## Duplicate protection and ambiguous starts

Migration `20261008000000_AddInPersonPayments` adds the in-person discriminator and a unique partial index enforcing one pending in-person payment per order across providers. It retains checkout-ID uniqueness for nonempty checkout IDs; in-person payments use the existing provider payment reference field without inventing a checkout session. Keep the ordinary order-payment index for historical queries.

The service locks the saved order, rejects already-paid orders, and commits a pending reservation before calling the provider. Concurrent/repeated starts replay that reservation without another provider call. While a Card attempt is pending, Cash/Other recording is rejected too. Fulfillment transitions remain independent of payment state.

Timeouts, disconnects, unknown responses, and process interruption cannot safely prove that a provider did not initiate collection. They retain the pending reservation and stable key. A repeat returns that existing attempt; it cannot initiate a second charge. A verified failure or paid event resolves the reservation. A crash before provider delivery can therefore leave an attempt pending: provider reconciliation/recovery belongs with the eventual real adapter, and pending attempts must not be manually treated as failed just to retry. Apply a forward fix rather than downgrading the migration after creating in-person records.
