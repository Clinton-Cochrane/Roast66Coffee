using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260823000000_AddPromotionsAndOrderPriceSnapshots")]
public partial class AddPromotionsAndOrderPriceSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("promotion_type", "menuitems", "integer", nullable: true);
        migrationBuilder.AddColumn<decimal>("promotion_value", "menuitems", "numeric(10,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>("unit_price", "orderitems", "numeric(10,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("unit_price", "addons", "numeric(10,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.Sql("UPDATE orderitems oi SET unit_price = m.price FROM menuitems m WHERE m.id = oi.menuitemid;");
        migrationBuilder.Sql("UPDATE addons a SET unit_price = m.price FROM menuitems m WHERE m.id = a.menuitemid;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("promotion_type", "menuitems");
        migrationBuilder.DropColumn("promotion_value", "menuitems");
        migrationBuilder.DropColumn("unit_price", "orderitems");
        migrationBuilder.DropColumn("unit_price", "addons");
    }
}
