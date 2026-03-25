using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Supabase: explicit RLS policies for anon/authenticated (tables already had RLS enabled).
    /// Drops unused FK indexes on menuitemid in the database (performance advisor). The EF model
    /// still includes those FK indexes so tooling stays aligned; only the physical indexes are removed.
    /// </summary>
    public partial class AddRlsPoliciesAndDropUnusedIndexes : Migration
    {
        private const string DenyPolicyName = "Deny_supabase_client_access";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS: safe if indexes were already dropped. Npgsql stores unquoted names as lowercase.
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_addons_menuitemid;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_orderitems_menuitemid;");

            // Explicit deny for PostgREST roles; .NET API uses a DB user that bypasses RLS.
            foreach (var table in new[]
                     {
                         "public.\"__EFMigrationsHistory\"",
                         "public.addons",
                         "public.menuitems",
                         "public.notificationsettings",
                         "public.orderitems",
                         "public.orders",
                     })
            {
                migrationBuilder.Sql(
                    "DROP POLICY IF EXISTS \"" + DenyPolicyName + "\" ON " + table + "; "
                    + "CREATE POLICY \"" + DenyPolicyName + "\" ON " + table
                    + " FOR ALL TO anon, authenticated USING (false) WITH CHECK (false);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
                     {
                         "public.\"__EFMigrationsHistory\"",
                         "public.addons",
                         "public.menuitems",
                         "public.notificationsettings",
                         "public.orderitems",
                         "public.orders",
                     })
            {
                migrationBuilder.Sql("DROP POLICY IF EXISTS \"" + DenyPolicyName + "\" ON " + table + ";");
            }

            migrationBuilder.CreateIndex(
                name: "IX_orderitems_menuitemid",
                table: "orderitems",
                column: "menuitemid");

            migrationBuilder.CreateIndex(
                name: "IX_addons_menuitemid",
                table: "addons",
                column: "menuitemid");
        }
    }
}
