#if NET8_0_OR_GREATER
using System;
using BililiveRecorder.Core.Config.Persistence.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BililiveRecorder.Core.Config.Persistence.Database.Migrations
{
    [DbContext(typeof(BilibiliRecorderDbContext))]
    [Migration("20250125020000_MigrateToHybridSchema")]
    partial class MigrateToHybridSchema
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("recorder")
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            // This migration doesn't change the model structure, only migrates data
            // Model is the same as AddHybridSchema migration

            // Old ConfigEntity (deprecated)
            modelBuilder.Entity("BililiveRecorder.Core.Config.Persistence.Database.ConfigEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("GlobalConfigJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("global_config");

                    b.Property<string>("RoomsConfigJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("rooms_config");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("Version")
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("UpdatedAt");

                    b.ToTable("config", "recorder");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            CreatedAt = new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc),
                            GlobalConfigJson = "{}",
                            RoomsConfigJson = "[]",
                            UpdatedAt = new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc),
                            Version = 3
                        });
                });

            // GlobalConfigEntity
            modelBuilder.Entity("BililiveRecorder.Core.Config.Persistence.Database.Entities.GlobalConfigEntity", b =>
                {
                    b.Property<string>("Key")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)")
                        .HasColumnName("key");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at")
                        .HasDefaultValueSql("NOW()");

                    b.Property<string>("Description")
                        .HasColumnType("text")
                        .HasColumnName("description");

                    b.Property<DateTime>("UpdatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at")
                        .HasDefaultValueSql("NOW()");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("value");

                    b.Property<string>("ValueType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("value_type");

                    b.HasKey("Key");

                    b.ToTable("global_config", "recorder");
                });

            // RoomEntity
            modelBuilder.Entity("BililiveRecorder.Core.Config.Persistence.Database.Entities.RoomEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<bool>("AutoRecord")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true)
                        .HasColumnName("auto_record");

                    b.Property<string>("ConfigOverrides")
                        .HasColumnType("jsonb")
                        .HasColumnName("config_overrides");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at")
                        .HasDefaultValueSql("NOW()");

                    b.Property<bool>("Enabled")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true)
                        .HasColumnName("enabled");

                    b.Property<int>("Platform")
                        .HasColumnType("integer")
                        .HasColumnName("platform");

                    b.Property<string>("RoomUrl")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("room_url");

                    b.Property<DateTime>("UpdatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at")
                        .HasDefaultValueSql("NOW()");

                    b.HasKey("Id");

                    b.HasIndex("AutoRecord")
                        .HasDatabaseName("IX_rooms_auto_record");

                    b.HasIndex("Platform")
                        .HasDatabaseName("IX_rooms_platform");

                    b.HasIndex("RoomUrl", "Platform")
                        .IsUnique()
                        .HasDatabaseName("IX_rooms_room_url_platform");

                    b.ToTable("rooms", "recorder");
                });
#pragma warning restore 612, 618
        }
    }
}
#endif
