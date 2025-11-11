#if NET8_0_OR_GREATER
using System;
using BililiveRecorder.Core.Config.Persistence.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence.Database.Migrations
{
    [DbContext(typeof(BilibiliRecorderDbContext))]
    [Migration("20250125000000_InitialCreate")]
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasDefaultSchema("recorder");

            modelBuilder.Entity("BililiveRecorder.Core.Config.Persistence.Database.ConfigEntity", b =>
                {
                    b.Property<int>("Id")
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

                    b.HasIndex("UpdatedAt")
                        .HasDatabaseName("IX_config_updated_at");

                    b.ToTable("config", "recorder");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            CreatedAt = new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                            GlobalConfigJson = "{}",
                            RoomsConfigJson = "[]",
                            UpdatedAt = new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                            Version = 3
                        });
                });
#pragma warning restore 612, 618
        }
    }
}
#endif
