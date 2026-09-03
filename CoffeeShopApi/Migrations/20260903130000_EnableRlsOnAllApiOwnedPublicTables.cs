using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

/// <summary>
/// Closes RLS gaps on API-owned public tables without depending on provider roles.
/// PostgreSQL denies non-owner access when RLS is enabled and no policy applies.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903130000_EnableRlsOnAllApiOwnedPublicTables")]
public partial class EnableRlsOnAllApiOwnedPublicTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $enable_rls$
            DECLARE
                api_table record;
            BEGIN
                FOR api_table IN
                    SELECT relation.relname
                    FROM pg_class AS relation
                    INNER JOIN pg_namespace AS schema
                        ON schema.oid = relation.relnamespace
                    WHERE schema.nspname = 'public'
                      AND relation.relkind IN ('r', 'p')
                      AND relation.relowner = (
                          SELECT role.oid
                          FROM pg_roles AS role
                          WHERE role.rolname = current_user)
                LOOP
                    EXECUTE format(
                        'ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY',
                        api_table.relname);
                END LOOP;
            END
            $enable_rls$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These are the only API-owned tables that lacked RLS immediately before
        // this migration. Existing protection on every other table must remain.
        migrationBuilder.Sql(
            "ALTER TABLE public.notificationmessages DISABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql(
            "ALTER TABLE public.payments DISABLE ROW LEVEL SECURITY;");
    }
}
