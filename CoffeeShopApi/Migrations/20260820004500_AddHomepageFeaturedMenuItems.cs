using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260820004500_AddHomepageFeaturedMenuItems")]
    public partial class AddHomepageFeaturedMenuItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_featured_on_home",
                table: "menuitems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE menuitems
                SET is_featured_on_home = TRUE
                WHERE name IN ('Mrs. Brownie Latte', 'Shitbox LUV Fuel', 'Black SS Lemonade');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_featured_on_home",
                table: "menuitems");
        }
    }
}
