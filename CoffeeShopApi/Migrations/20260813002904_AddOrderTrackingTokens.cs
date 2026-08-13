using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTrackingTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trackingtoken",
                table: "orders",
                type: "character varying(43)",
                maxLength: 43,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE orders
                SET trackingtoken = substring(
                    replace(gen_random_uuid()::text, '-', '') ||
                    replace(gen_random_uuid()::text, '-', '')
                    from 1 for 43)
                WHERE trackingtoken IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "trackingtoken",
                table: "orders",
                type: "character varying(43)",
                maxLength: 43,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(43)",
                oldMaxLength: 43,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_trackingtoken",
                table: "orders",
                column: "trackingtoken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_trackingtoken",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "trackingtoken",
                table: "orders");
        }
    }
}
