using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260827000000_GeneralizePaymentProviders")]
public partial class GeneralizePaymentProviders : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_paymentcheckoutdrafts",
            table: "paymentcheckoutdrafts");

        migrationBuilder.RenameTable(
            name: "paymentcheckoutdrafts",
            newName: "payments");

        migrationBuilder.RenameColumn(
            name: "checkoutsessionid",
            table: "payments",
            newName: "providercheckoutid");

        migrationBuilder.RenameColumn(
            name: "stripepaymentintentid",
            table: "payments",
            newName: "providerpaymentid");

        migrationBuilder.RenameColumn(
            name: "stripepaymentintentid",
            table: "orders",
            newName: "paymentreference");

        migrationBuilder.AddColumn<string>(
            name: "provider",
            table: "payments",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "stripe");

        migrationBuilder.AddColumn<string>(
            name: "method",
            table: "payments",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "amount",
            table: "payments",
            type: "numeric(10,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "currency",
            table: "payments",
            type: "character varying(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "USD");

        migrationBuilder.AddColumn<DateTime>(
            name: "refundedutc",
            table: "payments",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "confirmedbystaffutc",
            table: "payments",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "paymentprovider",
            table: "orders",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "status",
            table: "payments",
            type: "character varying(24)",
            maxLength: 24,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.Sql(
            "UPDATE payments SET status = 'paid' WHERE status = 'completed';");
        migrationBuilder.Sql(
            "UPDATE orders SET paymentprovider = 'stripe' WHERE paymentreference IS NOT NULL;");
        migrationBuilder.Sql(
            "UPDATE payments p SET orderid = NULL WHERE orderid IS NOT NULL AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.id = p.orderid);");

        migrationBuilder.AddPrimaryKey(
            name: "PK_payments",
            table: "payments",
            column: "id");

        migrationBuilder.CreateIndex(
            name: "IX_payments_orderid",
            table: "payments",
            column: "orderid");

        migrationBuilder.CreateIndex(
            name: "IX_payments_provider_idempotencykey",
            table: "payments",
            columns: new[] { "provider", "idempotencykey" });

        migrationBuilder.CreateIndex(
            name: "IX_payments_provider_providercheckoutid",
            table: "payments",
            columns: new[] { "provider", "providercheckoutid" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_payments_orders_orderid",
            table: "payments",
            column: "orderid",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_payments_orders_orderid",
            table: "payments");

        migrationBuilder.DropPrimaryKey(
            name: "PK_payments",
            table: "payments");

        migrationBuilder.DropIndex(
            name: "IX_payments_orderid",
            table: "payments");

        migrationBuilder.DropIndex(
            name: "IX_payments_provider_idempotencykey",
            table: "payments");

        migrationBuilder.DropIndex(
            name: "IX_payments_provider_providercheckoutid",
            table: "payments");

        migrationBuilder.DropColumn(name: "provider", table: "payments");
        migrationBuilder.DropColumn(name: "method", table: "payments");
        migrationBuilder.DropColumn(name: "amount", table: "payments");
        migrationBuilder.DropColumn(name: "currency", table: "payments");
        migrationBuilder.DropColumn(name: "refundedutc", table: "payments");
        migrationBuilder.DropColumn(name: "confirmedbystaffutc", table: "payments");
        migrationBuilder.DropColumn(name: "paymentprovider", table: "orders");

        migrationBuilder.AlterColumn<string>(
            name: "status",
            table: "payments",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(24)",
            oldMaxLength: 24);

        migrationBuilder.Sql(
            "UPDATE payments SET status = 'completed' WHERE status = 'paid';");

        migrationBuilder.RenameColumn(
            name: "providercheckoutid",
            table: "payments",
            newName: "checkoutsessionid");

        migrationBuilder.RenameColumn(
            name: "providerpaymentid",
            table: "payments",
            newName: "stripepaymentintentid");

        migrationBuilder.RenameColumn(
            name: "paymentreference",
            table: "orders",
            newName: "stripepaymentintentid");

        migrationBuilder.RenameTable(
            name: "payments",
            newName: "paymentcheckoutdrafts");

        migrationBuilder.AddPrimaryKey(
            name: "PK_paymentcheckoutdrafts",
            table: "paymentcheckoutdrafts",
            column: "id");
    }
}
