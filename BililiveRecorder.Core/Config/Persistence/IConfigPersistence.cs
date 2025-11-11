using BililiveRecorder.Core.Config.V3;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence
{
    /// <summary>
    /// Interface for configuration persistence - supports both file and database storage
    /// </summary>
    public interface IConfigPersistence
    {
        /// <summary>
        /// Load configuration from storage
        /// </summary>
        ConfigV3? Load();

        /// <summary>
        /// Save configuration to storage
        /// </summary>
        bool Save(ConfigV3 config);

        /// <summary>
        /// Check if storage is available and initialized
        /// </summary>
        bool IsAvailable();

        /// <summary>
        /// Initialize storage (create tables, directories, etc.)
        /// </summary>
        bool Initialize();
    }
}
