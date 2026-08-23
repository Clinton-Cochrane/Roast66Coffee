using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260822000000_RemoveDuplicateWatermelonShot")]
    public partial class RemoveDuplicateWatermelonShot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE menuitems
                SET description = regexp_replace(description, '\s+shot\y', '', 'gi')
                WHERE "CategoryType" = 2 AND description ~* '\mshot\M';

                WITH watermelon_shots AS (
                    SELECT id, MIN(id) OVER () AS keeper_id
                    FROM menuitems
                    WHERE name = 'Watermelon Shot' AND "CategoryType" = 2
                )
                UPDATE addons
                SET menuitemid = watermelon_shots.keeper_id
                FROM watermelon_shots
                WHERE addons.menuitemid = watermelon_shots.id
                  AND watermelon_shots.id <> watermelon_shots.keeper_id;

                WITH watermelon_shots AS (
                    SELECT id, MIN(id) OVER () AS keeper_id
                    FROM menuitems
                    WHERE name = 'Watermelon Shot' AND "CategoryType" = 2
                )
                UPDATE orderitems
                SET menuitemid = watermelon_shots.keeper_id
                FROM watermelon_shots
                WHERE orderitems.menuitemid = watermelon_shots.id
                  AND watermelon_shots.id <> watermelon_shots.keeper_id;

                DELETE FROM menuitems
                WHERE name = 'Watermelon Shot'
                  AND "CategoryType" = 2
                  AND id <> (
                      SELECT MIN(id)
                      FROM menuitems
                      WHERE name = 'Watermelon Shot' AND "CategoryType" = 2
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removing accidental duplicate data is intentionally irreversible.
        }
    }
}
