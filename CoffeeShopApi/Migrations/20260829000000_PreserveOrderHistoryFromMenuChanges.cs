using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260829000000_PreserveOrderHistoryFromMenuChanges")]
public partial class PreserveOrderHistoryFromMenuChanges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_archived",
            table: "menuitems",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        AddSnapshotColumns(migrationBuilder, "orderitems");
        AddSnapshotColumns(migrationBuilder, "addons");

        migrationBuilder.Sql(
            """
            UPDATE orderitems AS line
            SET item_name = menu.name,
                item_description = COALESCE(menu.description, ''),
                item_category_type = menu."CategoryType"
            FROM menuitems AS menu
            WHERE menu.id = line.menuitemid;

            UPDATE addons AS line
            SET item_name = menu.name,
                item_description = COALESCE(menu.description, ''),
                item_category_type = menu."CategoryType"
            FROM menuitems AS menu
            WHERE menu.id = line.menuitemid;

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM orderitems
                    WHERE item_name IS NULL OR item_description IS NULL OR item_category_type IS NULL
                ) OR EXISTS (
                    SELECT 1 FROM addons
                    WHERE item_name IS NULL OR item_description IS NULL OR item_category_type IS NULL
                ) THEN
                    RAISE EXCEPTION 'Cannot backfill menu snapshots for every retained order line.';
                END IF;
            END $$;
            """);

        RequireSnapshotColumns(migrationBuilder, "orderitems");
        RequireSnapshotColumns(migrationBuilder, "addons");

        migrationBuilder.DropForeignKey(
            name: "FK_addons_menuitems_menuitemid",
            table: "addons");
        migrationBuilder.DropForeignKey(
            name: "FK_orderitems_menuitems_menuitemid",
            table: "orderitems");

        migrationBuilder.AlterColumn<int>(
            name: "menuitemid",
            table: "orderitems",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");
        migrationBuilder.AlterColumn<int>(
            name: "menuitemid",
            table: "addons",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddForeignKey(
            name: "FK_addons_menuitems_menuitemid",
            table: "addons",
            column: "menuitemid",
            principalTable: "menuitems",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
        migrationBuilder.AddForeignKey(
            name: "FK_orderitems_menuitems_menuitemid",
            table: "orderitems",
            column: "menuitemid",
            principalTable: "menuitems",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM orderitems WHERE menuitemid IS NULL)
                   OR EXISTS (SELECT 1 FROM addons WHERE menuitemid IS NULL) THEN
                    RAISE EXCEPTION 'Cannot restore required menu references after menu items were permanently deleted.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_addons_menuitems_menuitemid",
            table: "addons");
        migrationBuilder.DropForeignKey(
            name: "FK_orderitems_menuitems_menuitemid",
            table: "orderitems");

        migrationBuilder.AlterColumn<int>(
            name: "menuitemid",
            table: "orderitems",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
        migrationBuilder.AlterColumn<int>(
            name: "menuitemid",
            table: "addons",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_addons_menuitems_menuitemid",
            table: "addons",
            column: "menuitemid",
            principalTable: "menuitems",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(
            name: "FK_orderitems_menuitems_menuitemid",
            table: "orderitems",
            column: "menuitemid",
            principalTable: "menuitems",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.DropColumn("item_name", "orderitems");
        migrationBuilder.DropColumn("item_description", "orderitems");
        migrationBuilder.DropColumn("item_category_type", "orderitems");
        migrationBuilder.DropColumn("item_name", "addons");
        migrationBuilder.DropColumn("item_description", "addons");
        migrationBuilder.DropColumn("item_category_type", "addons");
        migrationBuilder.DropColumn("is_archived", "menuitems");
    }

    private static void AddSnapshotColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<string>(
            name: "item_name",
            table: table,
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "item_description",
            table: table,
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "item_category_type",
            table: table,
            type: "integer",
            nullable: true);
    }

    private static void RequireSnapshotColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AlterColumn<string>(
            name: "item_name",
            table: table,
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200,
            oldNullable: true);
        migrationBuilder.AlterColumn<string>(
            name: "item_description",
            table: table,
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500,
            oldNullable: true);
        migrationBuilder.AlterColumn<int>(
            name: "item_category_type",
            table: table,
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }
}
