using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShopApi.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable Row Level Security on all public tables (Supabase security recommendation).
            // With RLS enabled and no policies, PostgREST (anon/authenticated) access is blocked.
            // The .NET API uses a direct connection (typically postgres role) which bypasses RLS.
            migrationBuilder.Sql("ALTER TABLE public.\"__EFMigrationsHistory\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.notificationsettings ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.orders ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.addons ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.menuitems ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.orderitems ENABLE ROW LEVEL SECURITY;");

            migrationBuilder.AlterColumn<string>(
                name: "customerphone",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "customername",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "menuitems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "menuitems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE public.\"__EFMigrationsHistory\" DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.notificationsettings DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.orders DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.addons DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.menuitems DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE public.orderitems DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.AlterColumn<string>(
                name: "customerphone",
                table: "orders",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "customername",
                table: "orders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "menuitems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "menuitems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }
    }
}
