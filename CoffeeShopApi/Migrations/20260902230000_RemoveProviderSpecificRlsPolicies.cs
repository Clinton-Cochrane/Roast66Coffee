using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

/// <summary>
/// Removes deny policies that referenced roles supplied by the former database provider.
/// RLS remains enabled and PostgreSQL's no-policy behavior denies non-owner access.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902230000_RemoveProviderSpecificRlsPolicies")]
public partial class RemoveProviderSpecificRlsPolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $cleanup$
            DECLARE
                existing_policy record;
            BEGIN
                FOR existing_policy IN
                    SELECT schemaname, tablename, policyname
                    FROM pg_policies
                    WHERE policyname = 'Deny_supabase_client_access'
                LOOP
                    EXECUTE format(
                        'DROP POLICY %I ON %I.%I',
                        existing_policy.policyname,
                        existing_policy.schemaname,
                        existing_policy.tablename);
                END LOOP;
            END
            $cleanup$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Provider-specific roles and policies are intentionally not recreated.
    }
}
