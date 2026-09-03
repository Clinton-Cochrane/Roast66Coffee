using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// This historical migration attempted to remove menu foreign-key indexes based on a transient
    /// advisor result. RestoreMenuForeignKeyIndexes restores the physical indexes because PostgreSQL
    /// uses those relationships for menu deletion and the EF model requires them. The historical class
    /// name is retained because it is part of the applied migration history; provider-specific policies
    /// are intentionally not created.
    /// </summary>
    public partial class AddRlsPoliciesAndDropUnusedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS: safe if indexes were already dropped. Npgsql stores unquoted names as lowercase.
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_addons_menuitemid;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_orderitems_menuitemid;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
