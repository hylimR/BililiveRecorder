#if NET8_0_OR_GREATER
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BililiveRecorder.Core.Config.Persistence.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHybridSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create global_config table (key-value storage)
            migrationBuilder.CreateTable(
                name: "global_config",
                schema: "recorder",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    value_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_config", x => x.key);
                });

            // Create rooms table (hybrid: core columns + JSONB config_overrides)
            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "recorder",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    room_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    platform = table.Column<int>(type: "integer", nullable: false),
                    auto_record = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    config_overrides = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                });

            // Create indexes on rooms table
            migrationBuilder.CreateIndex(
                name: "IX_rooms_room_url_platform",
                schema: "recorder",
                table: "rooms",
                columns: new[] { "room_url", "platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooms_platform",
                schema: "recorder",
                table: "rooms",
                column: "platform");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_auto_record",
                schema: "recorder",
                table: "rooms",
                column: "auto_record");

            // Create GIN index on JSONB config_overrides (PostgreSQL-specific)
            migrationBuilder.Sql(
                @"CREATE INDEX IX_rooms_config_overrides ON recorder.rooms USING GIN(config_overrides);",
                suppressTransaction: false);

            // Note: Data migration from old 'config' table to new hybrid schema
            // will be handled in a separate migration step
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop GIN index
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS recorder.IX_rooms_config_overrides;",
                suppressTransaction: false);

            // Drop rooms table
            migrationBuilder.DropTable(
                name: "rooms",
                schema: "recorder");

            // Drop global_config table
            migrationBuilder.DropTable(
                name: "global_config",
                schema: "recorder");
        }
    }
}
#endif
