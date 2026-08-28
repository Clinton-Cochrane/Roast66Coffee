using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260827010000_GeneralizeSmsProvider")]
public partial class GeneralizeSmsProvider : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "twiliofromphonenumber",
            table: "notificationsettings",
            newName: "smsfromaddress");

        migrationBuilder.AddColumn<string>(
            name: "provider",
            table: "notificationmessages",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "provider",
            table: "notificationmessages");

        migrationBuilder.RenameColumn(
            name: "smsfromaddress",
            table: "notificationsettings",
            newName: "twiliofromphonenumber");
    }
}
