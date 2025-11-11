#if NET8_0_OR_GREATER
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BililiveRecorder.Core.Api;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence.Database.Entities
{
    /// <summary>
    /// Database entity for room configuration (hybrid model)
    /// Core queryable fields as columns, config overrides as JSONB
    /// </summary>
    [Table("rooms", Schema = "recorder")]
    public class RoomEntity
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Room URL (e.g., https://live.bilibili.com/123456)
        /// </summary>
        [Column("room_url")]
        [Required]
        [MaxLength(500)]
        public string RoomUrl { get; set; } = string.Empty;

        /// <summary>
        /// Streaming platform (0=Bilibili, 1=Douyin)
        /// </summary>
        [Column("platform")]
        [Required]
        public StreamingPlatform Platform { get; set; } = StreamingPlatform.Bilibili;

        /// <summary>
        /// Auto record when stream starts
        /// </summary>
        [Column("auto_record")]
        [Required]
        public bool AutoRecord { get; set; } = true;

        /// <summary>
        /// Room enabled/disabled
        /// </summary>
        [Column("enabled")]
        [Required]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Configuration overrides as JSON
        /// Only stores properties that override global config
        /// Stored as JSONB in PostgreSQL for indexing and querying
        ///
        /// Example: {"RecordingQuality":"20000","RecordDanmaku":false}
        /// </summary>
        [Column("config_overrides", TypeName = "jsonb")]
        public string? ConfigOverrides { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp
        /// </summary>
        [Column("updated_at")]
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
#endif
