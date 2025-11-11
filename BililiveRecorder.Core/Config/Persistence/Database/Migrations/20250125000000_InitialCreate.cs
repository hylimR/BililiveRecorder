#if NET8_0_OR_GREATER
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence.Database.Migrations
{
    /// <summary>
    /// Initial database migration - creates config table
    /// </summary>
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recorder");

            migrationBuilder.CreateTable(
                name: "config",
                schema: "recorder",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    global_config = table.Column<string>(type: "text", nullable: false),
                    rooms_config = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_config_updated_at",
                schema: "recorder",
                table: "config",
                column: "updated_at");

            // Insert default config row
            migrationBuilder.InsertData(
                schema: "recorder",
                table: "config",
                columns: new[] { "id", "version", "global_config", "rooms_config", "created_at", "updated_at" },
                values: new object[] { 1, 3, "{}", "[]", DateTime.UtcNow, DateTime.UtcNow });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "config",
                schema: "recorder");
        }
    }
}
#endif
