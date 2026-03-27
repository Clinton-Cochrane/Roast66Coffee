using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "phonenumber",
                table: "notificationsettings",
                newName: "adminphonenumber");

            migrationBuilder.AlterColumn<string>(
                name: "adminphonenumber",
                table: "notificationsettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "baristaphonenumber",
                table: "notificationsettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trailerphonenumber",
                table: "notificationsettings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notificationmessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    eventtype = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recipientrole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recipientphone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    templatekey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    orderid = table.Column<int>(type: "integer", nullable: true),
                    payloadjson = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    providermessageid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    lasterror = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attemptcount = table.Column<int>(type: "integer", nullable: false),
                    dedupkey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    createdutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sentutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificationmessages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificationmessages_dedupkey",
                table: "notificationmessages",
                column: "dedupkey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificationmessages");

            migrationBuilder.DropColumn(
                name: "baristaphonenumber",
                table: "notificationsettings");

            migrationBuilder.DropColumn(
                name: "trailerphonenumber",
                table: "notificationsettings");

            migrationBuilder.AlterColumn<string>(
                name: "adminphonenumber",
                table: "notificationsettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.RenameColumn(
                name: "adminphonenumber",
                table: "notificationsettings",
                newName: "phonenumber");
        }
    }
}
