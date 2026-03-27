using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailFirstNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customeremail",
                table: "orders",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "customernotificationoptin",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "adminemail",
                table: "notificationsettings",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "baristaemail",
                table: "notificationsettings",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "traileremail",
                table: "notificationsettings",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "channel",
                table: "notificationmessages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipientemail",
                table: "notificationmessages",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "customeremail",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customernotificationoptin",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "adminemail",
                table: "notificationsettings");

            migrationBuilder.DropColumn(
                name: "baristaemail",
                table: "notificationsettings");

            migrationBuilder.DropColumn(
                name: "traileremail",
                table: "notificationsettings");

            migrationBuilder.DropColumn(
                name: "channel",
                table: "notificationmessages");

            migrationBuilder.DropColumn(
                name: "recipientemail",
                table: "notificationmessages");
        }
    }
}
