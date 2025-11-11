#if NET8_0_OR_GREATER
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence.Database.Entities
{
    /// <summary>
    /// Database entity for global configuration key-value pairs
    /// </summary>
    [Table("global_config", Schema = "recorder")]
    public class GlobalConfigEntity
    {
        /// <summary>
        /// Configuration key
        /// </summary>
        [Key]
        [Column("key")]
        [MaxLength(255)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Configuration value (stored as string, converted based on ValueType)
        /// </summary>
        [Column("value")]
        [Required]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Type of the value: 'string', 'int', 'bool', 'uint', 'double', 'enum', 'json'
        /// </summary>
        [Column("value_type")]
        [Required]
        [MaxLength(50)]
        public string ValueType { get; set; } = "string";

        /// <summary>
        /// Optional description of the configuration key
        /// </summary>
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp
        /// </summary>
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
#endif
