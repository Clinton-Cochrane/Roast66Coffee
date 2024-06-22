using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMenuItemsToLowercase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_MenuItems_MenuItemId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MenuItems",
                table: "MenuItems");

            migrationBuilder.RenameTable(
                name: "MenuItems",
                newName: "menuitems");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "menuitems",
                newName: "price");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "menuitems",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "menuitems",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "menuitems",
                newName: "id");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "menuitems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "menuitems",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_menuitems",
                table: "menuitems",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_menuitems_MenuItemId",
                table: "Orders",
                column: "MenuItemId",
                principalTable: "menuitems",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_menuitems_MenuItemId",
                table: "Orders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_menuitems",
                table: "menuitems");

            migrationBuilder.RenameTable(
                name: "menuitems",
                newName: "MenuItems");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "MenuItems",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "MenuItems",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "MenuItems",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "MenuItems",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MenuItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MenuItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MenuItems",
                table: "MenuItems",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Password = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_MenuItems_MenuItemId",
                table: "Orders",
                column: "MenuItemId",
                principalTable: "MenuItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
