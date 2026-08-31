using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830000000_AddOrderIdempotency")]
public partial class AddOrderIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "idempotencykey",
            table: "orders",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "requestfingerprint",
            table: "orders",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_orders_idempotency_key",
            table: "orders",
            column: "idempotencykey",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_orders_idempotency_key",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "idempotencykey",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "requestfingerprint",
            table: "orders");
    }
}
