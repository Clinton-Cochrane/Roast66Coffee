# Admin order history contract

The admin order list is an operational queue, not an analytics or permanent
business-history API. Both authenticated list routes use the same bounded
contract:

- `GET /api/admin/orders`
- `GET /api/order` (legacy route)

## Pagination and ordering

Each page contains exactly 50 slots; callers cannot request a different page
size. This fixed size matches the expected load of about 12 orders per normal
day and no more than about 50 per day while keeping the API, UI, and support
instructions simple. Reconsider the fixed size if normal volume approaches 50
orders per day, staff regularly need more than two pages, or response latency
and payload measurements justify a smaller default.

Orders are ordered deterministically:

1. active orders before completed orders;
2. newest `OrderDate` first within each group;
3. highest order ID first when timestamps match.

The response includes `items`, `page`, `pageSize`, `totalItems`, `totalPages`,
`hasPreviousPage`, and `hasNextPage`. Pages are one-based; a page below one is
invalid. A page beyond the last page returns an empty item list with accurate
metadata so clients can recover after live data moves or expires.

## Retention and timestamps

Completed orders remain visible for 30 hours after their transition to
`Completed`. `CompletedUtc`, not `OrderDate`, drives this window. Reopening an
order clears `CompletedUtc`; completing it again records a new timestamp.

The migration backfills pre-existing completed orders with
`CompletedUtc = OrderDate`. This prevents old completed orders from all
reappearing for 30 hours after deployment. This is visibility retention only;
issue 167 does not delete or anonymize business data.

## Filters and search

Supported query parameters are:

- `page`: one-based page number;
- `status`: `all`, `active`, `received`, `preparing`, `readyForPickup`, or
  `completed`;
- `fromUtc`: inclusive lower bound on `OrderDate`;
- `toUtc`: exclusive upper bound on `OrderDate`;
- `search`: case-insensitive order/customer/drink search.

Search matches an exact numeric order ID, partial customer name, normalized
phone number, stored drink snapshot name, or stored add-on/flavor snapshot
name. Date filters always use `OrderDate`; the 30-hour completed-order window
still applies to every search and filter combination.

Requests such as “all Superman orders in the last 30 days” cannot return old
completed orders from this operational view. Long-range anonymous drink/time
analytics belong to a separate endpoint and data model.

## Query boundary

The list query is read-only and projects only fields needed by the admin UI.
It uses stored order-line and add-on snapshots rather than joining current menu
entities. One count query and one page query are executed regardless of the
number of returned orders; no per-order or per-line queries are allowed.
