#if NET8_0_OR_GREATER
using Microsoft.EntityFrameworkCore;
using BililiveRecorder.Core.Config.Persistence.Database.Entities;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence.Database
{
    /// <summary>
    /// Entity Framework DbContext for BililiveRecorder
    /// </summary>
    public class BilibiliRecorderDbContext : DbContext
    {
        // Old JSON blob config table (deprecated, kept for migration compatibility)
        public DbSet<ConfigEntity> Configs { get; set; } = null!;

        // New hybrid schema tables
        public DbSet<GlobalConfigEntity> GlobalConfigs { get; set; } = null!;
        public DbSet<RoomEntity> Rooms { get; set; } = null!;

        public BilibiliRecorderDbContext(DbContextOptions<BilibiliRecorderDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Set default schema
            modelBuilder.HasDefaultSchema("recorder");

            // Configure old ConfigEntity (deprecated)
            modelBuilder.Entity<ConfigEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UpdatedAt);

                // Ensure only one config record exists (single-row table pattern)
                entity.HasData(new ConfigEntity
                {
                    Id = 1,
                    Version = 3,
                    GlobalConfigJson = "{}",
                    RoomsConfigJson = "[]"
                });
            });

            // Configure GlobalConfigEntity
            modelBuilder.Entity<GlobalConfigEntity>(entity =>
            {
                entity.HasKey(e => e.Key);
                entity.Property(e => e.Key).HasMaxLength(255);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.ValueType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Description);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            // Configure RoomEntity
            modelBuilder.Entity<RoomEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.RoomUrl).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Platform).IsRequired();
                entity.Property(e => e.AutoRecord).IsRequired().HasDefaultValue(true);
                entity.Property(e => e.Enabled).IsRequired().HasDefaultValue(true);
                entity.Property(e => e.ConfigOverrides).HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

                // Unique constraint on room_url + platform
                entity.HasIndex(e => new { e.RoomUrl, e.Platform })
                    .IsUnique()
                    .HasDatabaseName("IX_rooms_room_url_platform");

                // Index on platform for queries
                entity.HasIndex(e => e.Platform)
                    .HasDatabaseName("IX_rooms_platform");

                // Index on auto_record for queries
                entity.HasIndex(e => e.AutoRecord)
                    .HasDatabaseName("IX_rooms_auto_record");

                // GIN index on JSONB config_overrides (PostgreSQL-specific)
                // This will be added via raw SQL in migration
            });
        }
    }
}
#endif
