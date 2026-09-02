# Menu foreign-key index rationale

`orderitems.menuitemid` and `addons.menuitemid` retain conventional, non-unique
B-tree indexes. The foreign keys use `ON DELETE SET NULL` so historical order
snapshots survive menu deletion and bulk replacement. PostgreSQL must find every
referencing row when a menu item is deleted; without these indexes that work
degrades to full scans of both history tables.

The indexes also keep the migrated PostgreSQL schema aligned with the EF model.
An unused-index advisor observation is not sufficient evidence to remove them:
test traffic does not exercise menu maintenance at production-like history
cardinality, and PostgreSQL does not automatically index foreign-key columns.

## Synthetic PostgreSQL 17 check

The decision was checked on PostgreSQL 17 with a disposable database migrated
through `20260902000000_RestoreMenuForeignKeyIndexes`. The fixed synthetic shape
was:

- 100 menu items;
- 25,000 orders spread across active and completed states;
- 100,000 order items, four per order;
- 200,000 add-ons, two per order item; and
- 25 percent null `menuitemid` values to represent retained history after menu
  deletion.

Rows were inserted with `generate_series`, followed by `ANALYZE` on all four
tables. Plans used `EXPLAIN (ANALYZE, BUFFERS)` and compared the same warm-cache
queries before and after dropping only the two menu foreign-key indexes. The
figures below are evidence of plan shape and relative work from one local run,
not performance thresholds for CI.

| Query path | With indexes | Without indexes |
| --- | --- | --- |
| Admin history, first 50 orders with lines and add-ons | 11.81 ms; relationship joins used `IX_orderitems_orderid` and `IX_addons_orderitemid` | Not repeated: the menu foreign-key indexes do not participate in this path |
| `orderitems WHERE menuitemid = 18` | Bitmap index scan; 2.24 ms; 1,002 buffers | Sequential scan over 100,000 rows; 25.46 ms; 1,210 buffers |
| `addons WHERE menuitemid = 18` | Bitmap index scan; 4.40 ms; 2,003 buffers | Sequential scan over 200,000 rows; 35.89 ms; 2,083 buffers |
| Delete menu item 18, rolled back | 38.16 ms total; FK triggers 25.41 ms and 12.58 ms | 519.25 ms total; FK triggers 482.43 ms and 36.56 ms |

The admin-history plan confirms that its own relationship indexes remain the
right tools for that query. The equality and deletion plans show why the menu
foreign-key indexes are separately necessary. A composite index would add
unused keys, while a partial index would create model complexity without a
demonstrated need at this scale, so the ordinary EF-convention indexes are the
smallest durable choice.

Automated PostgreSQL integration tests assert the names, columns, uniqueness,
predicate, validity, and readiness of both physical indexes for fresh and
upgraded schemas. They deliberately do not assert planner timings or exact plan
nodes, which can change across PostgreSQL versions and data distributions.
