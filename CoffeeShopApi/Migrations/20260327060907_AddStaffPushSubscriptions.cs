using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staffpushsubscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    auth = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    useridentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    useragent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    createdutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffpushsubscriptions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staffpushsubscriptions_endpoint",
                table: "staffpushsubscriptions",
                column: "endpoint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staffpushsubscriptions");
        }
    }
}
