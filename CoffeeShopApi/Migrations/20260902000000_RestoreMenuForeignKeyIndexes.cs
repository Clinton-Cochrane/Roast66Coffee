using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

/// <summary>
/// Restores the conventional indexes for menu foreign keys. PostgreSQL uses these
/// relationships when menu deletion sets retained order references to null, so a
/// transient unused-index advisor result is not a durable reason to remove them.
/// The canonical names and definitions intentionally match the EF model.
/// See docs/operations/menu-foreign-key-index-rationale.md for representative plans.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902000000_RestoreMenuForeignKeyIndexes")]
public partial class RestoreMenuForeignKeyIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Remove non-canonical lowercase aliases left by manual database changes.
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS public.ix_addons_menuitemid;
            DROP INDEX IF EXISTS public.ix_orderitems_menuitemid;
            """);

        // IF NOT EXISTS supports both affected databases and fresh databases where
        // the historical unquoted DROP did not match EF's quoted index names.
        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_addons_menuitemid"
                ON public.addons USING btree (menuitemid);
            CREATE INDEX IF NOT EXISTS "IX_orderitems_menuitemid"
                ON public.orderitems USING btree (menuitemid);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS public."IX_addons_menuitemid";
            DROP INDEX IF EXISTS public."IX_orderitems_menuitemid";
            """);
    }
}
