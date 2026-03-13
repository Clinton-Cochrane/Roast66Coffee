using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPhoneAndOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customerphone",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orderstatus",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE orders SET orderstatus = 3 WHERE \"Status\" = true");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "customerphone",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "orderstatus",
                table: "orders");

            migrationBuilder.AddColumn<bool>(
                name: "Status",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
