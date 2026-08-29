# Payment Rollout Runbook

## Current rollout status

Online payment is a release blocker until every item in the launch checklist below is complete.

- Frontend: `https://roast66coffee-frontend.onrender.com`
- API: `https://roast66coffee.onrender.com`
- Environment: one prelaunch Render environment; there is no separate staging deployment
- Database backup: the project owner confirmed a current backup on 2026-08-28
- Stripe account: not created yet
- Public domain: not configured yet
- Operational owner for payment support, refunds, reconciliation, and outages: client
- Alert destination: a client-owned private Discord channel

Do not set `VITE_ENABLE_ONLINE_PAYMENTS=true` until the Stripe account, webhook, alerts, and test-mode validation are complete.

## Payment behavior

An order is always created before payment. A customer can place and track an unpaid order even when Stripe is unavailable or disabled. Online checkout only settles an existing order; a successful verified webhook marks that order paid and records `stripe` as its payment provider.

The first launch should enable cards and eligible card wallets. Checkout leaves payment-method selection under Stripe Dashboard control so the client can accept additional methods later. Before enabling a delayed method, exercise its successful and failed asynchronous webhook paths and confirm that the customer-facing status remains accurate while payment is processing.

## Stripe and Render setup

1. The client owner creates and owns the Stripe account, completes identity and business verification, and connects the settlement bank account.
2. Start in Stripe test mode.
3. Enable cards and eligible wallets in Stripe payment-method settings.
4. Set these API environment variables in Render without placing secret values in the repository:
   - `Payments__DefaultProvider=stripe`
   - `Payments__FrontendBaseUrl=https://roast66coffee-frontend.onrender.com`
   - `Stripe__SecretKey=<Stripe secret key>`
   - `Stripe__WebhookSecret=<endpoint signing secret>`
5. Confirm `AllowedOrigins` contains `https://roast66coffee-frontend.onrender.com`.
6. Create the Stripe webhook endpoint:
   - `POST https://roast66coffee.onrender.com/api/payments/stripe/webhook`
7. Subscribe it to:
   - `checkout.session.completed`
   - `checkout.session.async_payment_succeeded`
   - `checkout.session.async_payment_failed`
   - `payment_intent.payment_failed`
8. Copy that endpoint's signing secret to `Stripe__WebhookSecret` and redeploy the API.
9. Create a webhook in a private client-owned Discord channel. Keep its URL out of the repository and store it only in the selected monitoring relay's secret store.
10. Configure the monitoring relay to notify that Discord channel about checkout errors, invalid signatures, retryable webhook failures, Stripe delivery failures, and paid payments without linked orders.
11. Train the client owner to find a payment, match it to an order, issue and record a refund, reconcile a payment, disable online payment during an outage, and recognize which Discord alerts require immediate action.

When the public domain is connected, update `AllowedOrigins`, `Payments__FrontendBaseUrl`, `VITE_API_URL`, the Stripe webhook URL, and this runbook together.

## Test-mode validation

Use the prelaunch Render environment and Stripe test mode:

1. Place an order without paying and confirm it immediately appears in the admin order list without a paid badge.
2. Open the order's private status link and start online payment.
3. Pay with a Stripe test card and confirm the customer returns to the same private order-status page.
4. Confirm the order changes to paid and the admin card shows `Paid · Stripe`.
5. Repeat the payment action and replay the successful webhook concurrently. Confirm there is no second charge, no duplicate local payment, and only the existing order is updated.
6. Send an invalid signature and confirm the endpoint returns `400`.
7. Exercise failed and delayed test payments before enabling any delayed payment method.
8. Confirm canceling Stripe leaves the order valid and unpaid.
9. Confirm provider credentials and provider reference values never appear in a public order response.

## Production enablement

1. Confirm a fresh database backup.
2. Confirm migrations `20260827000000_GeneralizePaymentProviders` and `20260828000000_AddPaymentConcurrencyToken` appear in `__EFMigrationsHistory`.
3. Repeat the webhook setup with Stripe live-mode keys and the live endpoint signing secret.
4. Complete a low-value live payment while the frontend flag is still disabled by starting checkout from a controlled test client.
5. Reconcile the Stripe payment, local payment record, and linked order.
6. Confirm the client owner has completed payment operations training and can receive a test Discord alert.
7. Set `VITE_ENABLE_ONLINE_PAYMENTS=true` and rebuild/redeploy the static frontend.

## Production domain

1. Connect the production domain to the frontend and API as planned.
2. Update `AllowedOrigins`, `Payments__FrontendBaseUrl`, and `VITE_API_URL` to the production origins.
3. Update the Stripe webhook endpoint if the API hostname changes.
4. Confirm checkout success and cancel returns use the production frontend domain.
5. Repeat signature, webhook delivery, payment, and unpaid-order outage checks after the domain change.

## Support, refunds, reconciliation, and outages

The client owns these operational decisions and the private Discord alert channel.

- Support: use the Stripe payment search and the admin order number to locate the payment. Never request or share secret keys.
- Refunds: the client performs refunds in Stripe Dashboard and records the refund in the client's operational log. The current application does not automatically synchronize Stripe refunds back into the paid badge.
- Reconciliation: compare Stripe successful payments with local paid payment records and linked orders. Treat any paid payment without a linked order as urgent.
- Provider outage: keep order creation available, set `VITE_ENABLE_ONLINE_PAYMENTS=false`, and redeploy the frontend. Existing orders remain valid and can be paid another way.
- Alerts: route Stripe webhook delivery failures and Render application alerts for checkout errors, invalid signatures, retryable webhook failures, and payments without linked orders to the private client Discord channel. Test the complete alert path before launch.

Prefer disabling online payment and applying a forward fix. Do not run the provider-generalization migration `Down` in production.
