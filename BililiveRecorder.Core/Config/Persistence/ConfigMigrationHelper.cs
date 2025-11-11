using System;
using BililiveRecorder.Core.Config.V3;
using Serilog;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence
{
    /// <summary>
    /// Helper class to migrate configuration between different persistence backends
    /// </summary>
    public static class ConfigMigrationHelper
    {
        /// <summary>
        /// Migrate configuration from file to database
        /// </summary>
        /// <param name="source">Source persistence (file)</param>
        /// <param name="target">Target persistence (database)</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>True if migration succeeded</returns>
        public static bool MigrateConfig(IConfigPersistence source, IConfigPersistence target, ILogger? logger = null)
        {
            var log = logger ?? Log.ForContext(typeof(ConfigMigrationHelper));

            try
            {
                log.Information("Starting config migration from {Source} to {Target}",
                    source.GetType().Name, target.GetType().Name);

                // Load from source
                var config = source.Load();
                if (config == null)
                {
                    log.Warning("No config found in source, creating default config");
                    config = new ConfigV3();
                }

                log.Information("Loaded config from source: {RoomCount} rooms", config.Rooms.Count);

                // Initialize target if needed
                if (!target.IsAvailable())
                {
                    log.Information("Target persistence not available, initializing...");
                    if (!target.Initialize())
                    {
                        log.Error("Failed to initialize target persistence");
                        return false;
                    }
                }

                // Save to target
                if (!target.Save(config))
                {
                    log.Error("Failed to save config to target");
                    return false;
                }

                log.Information("Config migration completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Config migration failed");
                return false;
            }
        }

        /// <summary>
        /// Create a backup of config to file before migrating
        /// </summary>
        public static bool BackupToFile(ConfigV3 config, string backupPath, ILogger? logger = null)
        {
            var log = logger ?? Log.ForContext(typeof(ConfigMigrationHelper));

            try
            {
                var json = ConfigParser.SaveJson(config);
                if (json != null)
                {
                    System.IO.File.WriteAllText(backupPath, json);
                    log.Information("Config backup created at {BackupPath}", backupPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to create config backup");
                return false;
            }
        }
    }
}
