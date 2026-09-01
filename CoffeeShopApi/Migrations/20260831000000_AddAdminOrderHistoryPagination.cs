using System;
using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260831000000_AddAdminOrderHistoryPagination")]
public partial class AddAdminOrderHistoryPagination : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "completedutc",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        // Existing rows have no completion event timestamp. OrderDate is the
        // conservative backfill: old completed orders stay outside the 30-hour
        // operational window instead of all reappearing after deployment.
        migrationBuilder.Sql(
            "UPDATE orders SET completedutc = orderdate WHERE orderstatus = 3 AND completedutc IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_orders_admin_history",
            table: "orders",
            columns: new[] { "orderstatus", "completedutc", "orderdate", "id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_orders_admin_history",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "completedutc",
            table: "orders");
    }
}
