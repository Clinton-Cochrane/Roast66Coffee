using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOrdersToLowercase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_menuitems_MenuItemId",
                table: "Orders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Orders",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Orders");

            migrationBuilder.RenameTable(
                name: "Orders",
                newName: "orders");

            migrationBuilder.RenameColumn(
                name: "OrderDate",
                table: "orders",
                newName: "orderdate");

            migrationBuilder.RenameColumn(
                name: "MenuItemId",
                table: "orders",
                newName: "menuitemid");

            migrationBuilder.RenameColumn(
                name: "CustomerName",
                table: "orders",
                newName: "customername");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "orders",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_MenuItemId",
                table: "orders",
                newName: "IX_orders_menuitemid");

            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_orders",
                table: "orders",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_menuitems_menuitemid",
                table: "orders",
                column: "menuitemid",
                principalTable: "menuitems",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_menuitems_menuitemid",
                table: "orders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "orders");

            migrationBuilder.RenameTable(
                name: "orders",
                newName: "Orders");

            migrationBuilder.RenameColumn(
                name: "orderdate",
                table: "Orders",
                newName: "OrderDate");

            migrationBuilder.RenameColumn(
                name: "menuitemid",
                table: "Orders",
                newName: "MenuItemId");

            migrationBuilder.RenameColumn(
                name: "customername",
                table: "Orders",
                newName: "CustomerName");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Orders",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_orders_menuitemid",
                table: "Orders",
                newName: "IX_Orders_MenuItemId");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Orders",
                table: "Orders",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_menuitems_MenuItemId",
                table: "Orders",
                column: "MenuItemId",
                principalTable: "menuitems",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
