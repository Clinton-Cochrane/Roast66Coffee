using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDataRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE notificationmessages
                SET payloadjson = CASE
                        WHEN orderid IS NULL THEN '{}'
                        ELSE json_build_object('orderId', orderid)::text
                    END,
                    lasterror = CASE
                        WHEN lasterror IS NULL THEN NULL
                        ELSE 'Legacy notification failure details removed.'
                    END,
                    dedupkey = 'legacy-' || replace(id::text, '-', '');
                """);

            migrationBuilder.DropColumn(
                name: "recipientemail",
                table: "notificationmessages");

            migrationBuilder.DropColumn(
                name: "recipientphone",
                table: "notificationmessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recipientemail",
                table: "notificationmessages",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recipientphone",
                table: "notificationmessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }
    }
}
