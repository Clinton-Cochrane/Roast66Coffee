# Logging and Data Retention

This policy covers Roast 66 application request logs and notification-delivery
audit records. Tracking tokens, JWTs, connection strings, notification
credentials, and provider secrets are credentials and must never be copied into
logs, tickets, chat, or retained diagnostic files.

## Log inventory

| Store | Operational fields | PII or provider data | Retention |
| --- | --- | --- | --- |
| API request and application logs | HTTP method, route template, status code, latency, trace ID, safe failure type, internal order/payment ID, counts | None intentionally recorded. Raw paths, query strings, request/response bodies, exception messages, tracking tokens, credentials, and provider references are excluded. | Configure the centralized logging provider for no more than 30 days. Do not export logs to an unmanaged store. |
| `notificationmessages` database audit | Internal order ID, event/template, channel, recipient role, delivery status, attempt count, timestamps, provider name/message ID, safe failure classification | Recipient phone or email is retained only for delivery and support correlation. `PayloadJson` contains only the internal order ID. | The API purges every channel after 30 days. The worker checks every 12 hours; an admin can also call `POST /api/admin/notifications/purge-logs`. |
| Email, SMS, push, and payment provider dashboards | Provider-managed delivery or payment records | May contain customer destinations, message content, payment references, or provider diagnostics. | Configure provider-side retention to 30 days or less where supported. Review this setting quarterly and do not copy provider payloads into application logs. |

Orders and payments are business records rather than application logs. Their
retention and deletion require a separate business-data policy.

## Request-log correlation

Request events use the ASP.NET route template, never the concrete path. For
example, a tracking request is recorded as
`api/order/track/{trackingToken}`. Use the trace ID to correlate failures across
request events and use an internal order or payment ID when the application
already has one. Do not add a raw tracking token or provider reference for
correlation.

Unknown routes are recorded as `<unmatched>` so attacker-controlled path values
do not enter the log. Query strings are never recorded.

## Operational checks

After a logging or routing change:

1. Run `dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj`.
2. Exercise a private tracking URL containing a recognizable test token.
3. Confirm the request event contains the route template, status, latency, and
   trace ID but not the test token or query string.
4. Trigger a provider failure with test data and confirm only its HTTP status or
   safe failure type is logged, not the response body.
5. Confirm the centralized log retention remains at or below 30 days.

## Exposure response

If a credential or customer identifier is found in logs, restrict access to the
affected log store, determine the exposure window, delete affected exports when
the platform permits, and rotate the exposed credential. Treat an exposed
tracking token as a compromised private order link. Record the incident without
copying the sensitive value into the incident record, then rerun the sensitive
logging tests before redeployment.
