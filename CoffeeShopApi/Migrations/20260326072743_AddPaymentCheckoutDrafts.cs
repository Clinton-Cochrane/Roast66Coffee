using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentCheckoutDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "paymentcheckoutdrafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkoutsessionid = table.Column<string>(type: "text", nullable: false),
                    idempotencykey = table.Column<string>(type: "text", nullable: false),
                    customername = table.Column<string>(type: "text", nullable: false),
                    customerphone = table.Column<string>(type: "text", nullable: false),
                    payloadjson = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    createdutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completedutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failedutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stripepaymentintentid = table.Column<string>(type: "text", nullable: true),
                    orderid = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paymentcheckoutdrafts", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paymentcheckoutdrafts");
        }
    }
}
