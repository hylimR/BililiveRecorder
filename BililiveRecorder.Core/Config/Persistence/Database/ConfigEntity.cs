#if NET8_0_OR_GREATER
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence.Database
{
    /// <summary>
    /// Database entity for storing configuration
    /// Uses single-row table pattern - only one config record exists
    /// </summary>
    [Table("config", Schema = "recorder")]
    public class ConfigEntity
    {
        /// <summary>
        /// Primary key - always 1 (single row table)
        /// </summary>
        [Key]
        [Column("id")]
        public int Id { get; set; } = 1;

        /// <summary>
        /// Configuration version
        /// </summary>
        [Column("version")]
        [Required]
        public int Version { get; set; }

        /// <summary>
        /// Global configuration as JSON
        /// </summary>
        [Column("global_config")]
        [Required]
        public string GlobalConfigJson { get; set; } = string.Empty;

        /// <summary>
        /// Rooms configuration as JSON array
        /// </summary>
        [Column("rooms_config")]
        [Required]
        public string RoomsConfigJson { get; set; } = "[]";

        /// <summary>
        /// Last update timestamp
        /// </summary>
        [Column("updated_at")]
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Creation timestamp
        /// </summary>
        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
#endif
