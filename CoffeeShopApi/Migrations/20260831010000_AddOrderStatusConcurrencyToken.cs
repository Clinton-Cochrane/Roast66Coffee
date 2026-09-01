using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260831010000_AddOrderStatusConcurrencyToken")]
public partial class AddOrderStatusConcurrencyToken : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "statusconcurrencytoken",
            table: "orders",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "statusconcurrencytoken",
            table: "orders");
    }
}
