using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOrderQuantityBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_orderitems_quantity_range",
                table: "orderitems",
                sql: "quantity >= 1 AND quantity <= 12");

            migrationBuilder.AddCheckConstraint(
                name: "ck_addons_quantity_range",
                table: "addons",
                sql: "quantity >= 1 AND quantity <= 12");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orderitems_quantity_range",
                table: "orderitems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_addons_quantity_range",
                table: "addons");
        }
    }
}
