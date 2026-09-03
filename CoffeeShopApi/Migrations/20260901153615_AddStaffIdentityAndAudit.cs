using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffIdentityAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "staffuserid",
                table: "staffpushsubscriptions",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "auditevents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurredutc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actoruserid = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    actordisplayname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entitytype = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entityid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    detailsjson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditevents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staffroles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffroles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staffusers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffusers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staffroleclaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffroleclaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffroleclaims_staffroles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "staffroles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffuserclaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffuserclaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staffuserclaims_staffusers_UserId",
                        column: x => x.UserId,
                        principalTable: "staffusers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffuserlogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffuserlogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_staffuserlogins_staffusers_UserId",
                        column: x => x.UserId,
                        principalTable: "staffusers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffuserroles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffuserroles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_staffuserroles_staffroles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "staffroles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staffuserroles_staffusers_UserId",
                        column: x => x.UserId,
                        principalTable: "staffusers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staffusertokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staffusertokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_staffusertokens_staffusers_UserId",
                        column: x => x.UserId,
                        principalTable: "staffusers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staffpushsubscriptions_staffuserid",
                table: "staffpushsubscriptions",
                column: "staffuserid");

            migrationBuilder.CreateIndex(
                name: "ix_auditevents_entity_action_occurredutc",
                table: "auditevents",
                columns: new[] { "entitytype", "entityid", "action", "occurredutc" });

            migrationBuilder.CreateIndex(
                name: "IX_staffroleclaims_RoleId",
                table: "staffroleclaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "staffroles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staffuserclaims_UserId",
                table: "staffuserclaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_staffuserlogins_UserId",
                table: "staffuserlogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_staffuserroles_RoleId",
                table: "staffuserroles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "staffusers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "staffusers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_staffpushsubscriptions_staffusers_staffuserid",
                table: "staffpushsubscriptions",
                column: "staffuserid",
                principalTable: "staffusers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            foreach (var table in new[]
                     {
                         "auditevents",
                         "staffroles",
                         "staffroleclaims",
                         "staffusers",
                         "staffuserclaims",
                         "staffuserlogins",
                         "staffuserroles",
                         "staffusertokens",
                         "staffpushsubscriptions"
                     })
            {
                migrationBuilder.Sql($"ALTER TABLE public.{table} ENABLE ROW LEVEL SECURITY;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE public.staffpushsubscriptions DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropForeignKey(
                name: "FK_staffpushsubscriptions_staffusers_staffuserid",
                table: "staffpushsubscriptions");

            migrationBuilder.DropTable(
                name: "auditevents");

            migrationBuilder.DropTable(
                name: "staffroleclaims");

            migrationBuilder.DropTable(
                name: "staffuserclaims");

            migrationBuilder.DropTable(
                name: "staffuserlogins");

            migrationBuilder.DropTable(
                name: "staffuserroles");

            migrationBuilder.DropTable(
                name: "staffusertokens");

            migrationBuilder.DropTable(
                name: "staffroles");

            migrationBuilder.DropTable(
                name: "staffusers");

            migrationBuilder.DropIndex(
                name: "IX_staffpushsubscriptions_staffuserid",
                table: "staffpushsubscriptions");

            migrationBuilder.DropColumn(
                name: "staffuserid",
                table: "staffpushsubscriptions");
        }
    }
}
