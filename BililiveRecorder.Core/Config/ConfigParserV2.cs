using System;
using BililiveRecorder.Core.Config.Persistence;
using BililiveRecorder.Core.Config.V3;
using Serilog;

#nullable enable
namespace BililiveRecorder.Core.Config
{
    /// <summary>
    /// Enhanced ConfigParser with pluggable persistence backend support
    /// Supports both file-based and database-based configuration storage
    /// </summary>
    public class ConfigParserV2
    {
        private static ConfigParserV2? globalInstance;

        private readonly IConfigPersistence persistence;
        private readonly ILogger logger;

        public ConfigParserV2(IConfigPersistence persistence, ILogger? logger = null)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.logger = logger?.ForContext<ConfigParserV2>() ?? Log.ForContext<ConfigParserV2>();
        }

        /// <summary>
        /// Load configuration from the configured persistence backend
        /// </summary>
        public ConfigV3? Load()
        {
            try
            {
                // Initialize persistence if needed
                if (!this.persistence.IsAvailable())
                {
                    this.logger.Information("Persistence backend not available, initializing...");
                    if (!this.persistence.Initialize())
                    {
                        this.logger.Error("Failed to initialize persistence backend");
                        return null;
                    }
                }

                return this.persistence.Load();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to load config");
                return null;
            }
        }

        /// <summary>
        /// Save configuration to the configured persistence backend
        /// </summary>
        public bool Save(ConfigV3 config)
        {
            try
            {
                return this.persistence.Save(config);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to save config");
                return false;
            }
        }

        /// <summary>
        /// Check if persistence backend is available
        /// </summary>
        public bool IsAvailable()
        {
            return this.persistence.IsAvailable();
        }

        /// <summary>
        /// Initialize persistence backend
        /// </summary>
        public bool Initialize()
        {
            return this.persistence.Initialize();
        }

        /// <summary>
        /// Set global instance for use by ConfigParser.Save() static method
        /// </summary>
        public static void SetGlobalInstance(ConfigParserV2 instance)
        {
            globalInstance = instance;
        }

        /// <summary>
        /// Get global instance (used by ConfigParser.Save() static method)
        /// </summary>
        internal static ConfigParserV2? GetGlobalInstance()
        {
            return globalInstance;
        }
    }
}
