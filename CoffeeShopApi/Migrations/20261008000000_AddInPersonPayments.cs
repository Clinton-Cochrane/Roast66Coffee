using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261008000000_AddInPersonPayments")]
public partial class AddInPersonPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "isinperson", table: "payments", type: "boolean",
            nullable: false, defaultValue: false);
        migrationBuilder.DropIndex(name: "IX_payments_provider_providercheckoutid", table: "payments");
        migrationBuilder.CreateIndex(name: "IX_payments_provider_providercheckoutid", table: "payments",
            columns: new[] { "provider", "providercheckoutid" }, unique: true, filter: "providercheckoutid <> ''");
        migrationBuilder.CreateIndex(name: "ix_payments_pending_in_person_order", table: "payments",
            column: "orderid", unique: true, filter: "isinperson = TRUE AND status = 'pending'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_payments_pending_in_person_order", table: "payments");
        migrationBuilder.DropIndex(name: "IX_payments_provider_providercheckoutid", table: "payments");
        migrationBuilder.DropColumn(name: "isinperson", table: "payments");
        migrationBuilder.CreateIndex(name: "IX_payments_provider_providercheckoutid", table: "payments",
            columns: new[] { "provider", "providercheckoutid" }, unique: true);
    }
}
