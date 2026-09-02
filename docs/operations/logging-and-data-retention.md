# Logging and Data Retention

This policy covers completed orders, their dependent records, application logs,
notification-delivery audit rows, staff audit events, and backups. Customer
contact information remains available to the live order and delivery workflow,
but it must not be copied into a long-lived operational log.

## Approved lifecycles

| Store | Allowed contents | Retention |
| --- | --- | --- |
| Active and incomplete orders | Customer name and opted-in contact data required by the live order workflow | No automatic purge. |
| Completed orders | The raw order, customer name/contact data, private tracking token, item/add-on snapshots, and related payment record | Delete at 30 hours after `CompletedUtc`. |
| Orphan payments | Payment records no longer related to an order | Delete at 30 hours after `CreatedUtc`. |
| `notificationmessages` | Internal order ID, event/template, channel, recipient role, delivery status, attempt count, timestamps, provider/message ID, and safe failure classification | Delete with its completed order or after 90 days, whichever happens first. Every channel and status is included. |
| `auditevents` | Staff actor, action, internal entity reference, timestamp, and reviewed structured details | Delete after 90 days. |
| API request and application logs | HTTP method, route template, status, latency, trace ID, counts, internal numeric IDs, and safe failure type | Configure the centralized provider for no more than 90 days. |
| Full database backups | Encrypted application data needed for disaster recovery | Delete before the backup reaches 30 hours old. |

Operational logs must not contain customer names, tracking tokens, email
addresses, phone numbers, IP addresses, raw paths or queries, request/response
bodies, exception messages, connection strings, credentials, or provider
response bodies. Staff display names in the staff-security audit are not
customer PII.

`notificationmessages` deliberately has no recipient phone or email columns.
The notification service keeps the destination in memory only while delivering
the message. `PayloadJson` contains only the internal numeric order ID.

Provider dashboards are delivery systems rather than application logs, but they
may contain destinations or message content. Configure them for 30 hours or the
shortest supported duration, whichever is shorter, and never export their raw
payloads into an operational log store.

## Automatic and manual purge

`DataRetentionWorker` runs at startup and every configured interval. The
production default is hourly. It uses bounded transactions and PostgreSQL
`FOR UPDATE SKIP LOCKED`, allowing overlapping API instances or a manual purge
to process different rows safely. A failed batch rolls back and is retryable;
previously committed batches remain deleted.

An Owner or Admin can request an immediate pass:

```bash
curl --fail --silent --show-error \
  -X POST \
  -H "Authorization: Bearer ${ROAST66_ADMIN_JWT}" \
  https://API_HOST/api/admin/retention/purge
```

The response contains deletion counts only. It must not contain deleted record
IDs or customer data. The two legacy notification-purge routes remain aliases
for compatibility.

## Verification

Run count-only queries using a protected database session. Do not select or
export the expired rows:

```sql
SELECT count(*) AS expired_completed_orders
FROM orders
WHERE orderstatus = 3
  AND completedutc <= (now() AT TIME ZONE 'utc') - interval '30 hours';

SELECT count(*) AS expired_orphan_payments
FROM payments
WHERE orderid IS NULL
  AND createdutc <= (now() AT TIME ZONE 'utc') - interval '30 hours';

SELECT count(*) AS expired_notification_logs
FROM notificationmessages
WHERE createdutc <= (now() AT TIME ZONE 'utc') - interval '90 days';

SELECT count(*) AS expired_audit_events
FROM auditevents
WHERE occurredutc <= (now() AT TIME ZONE 'utc') - interval '90 days';
```

All four counts should be zero after a successful pass. Confirm the application
event reports only per-category counts, batch count, and safe failure type.

## Backup requirements

Create full backups often enough that a verified replacement exists before the
previous backup reaches 30 hours, then delete the previous copy. Provider-managed
snapshots or point-in-time recovery windows longer than 30 hours are incompatible
with this policy while they contain customer/order data.

Every restore must remain isolated from customers until migrations run, an
immediate retention pass succeeds, and the count-only queries above return zero.
This restore-time purge is defense in depth; it does not permit retaining the
source backup beyond 30 hours.

Redacted log-only exports may be retained for up to 90 days, but they must not be
combined with raw order, payment, notification-destination, or provider data.

## Exposure response

If customer data or a credential is found in a log, restrict the affected store,
determine the exposure window without copying the value, delete affected exports
where possible, and rotate exposed credentials. Treat a logged tracking token as
a compromised private order link. Rerun the sensitive-logging and retention tests
before redeployment.
