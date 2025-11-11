using System;
using System.IO;
using BililiveRecorder.Core.Config.V3;
using Serilog;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence
{
    /// <summary>
    /// File-based configuration persistence (original behavior)
    /// </summary>
    public class FileConfigPersistence : IConfigPersistence
    {
        private readonly string directory;
        private readonly ILogger logger;

        public FileConfigPersistence(string directory, ILogger? logger = null)
        {
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
            this.logger = logger?.ForContext<FileConfigPersistence>() ?? Log.ForContext<FileConfigPersistence>();
        }

        public ConfigV3? Load()
        {
            try
            {
                var config = ConfigParser.LoadFromDirectory(this.directory);
                if (config != null)
                {
                    this.logger.Information("[File] Config loaded successfully from file: {RoomCount} rooms", config.Rooms.Count);
                }
                else
                {
                    this.logger.Warning("[File] Failed to load config from file");
                }
                return config;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "[File] Failed to load config from directory: {Directory}", this.directory);
                return null;
            }
        }

        public bool Save(ConfigV3 config)
        {
            try
            {
                var result = ConfigParser.Save(config);
                if (result)
                {
                    this.logger.Information("[File] Config saved to file successfully ({RoomCount} rooms)", config.Rooms.Count);
                }
                else
                {
                    this.logger.Warning("[File] Failed to save config to file");
                }
                return result;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "[File] Failed to save config to file");
                return false;
            }
        }

        public bool IsAvailable()
        {
            return Directory.Exists(this.directory);
        }

        public bool Initialize()
        {
            try
            {
                if (!Directory.Exists(this.directory))
                {
                    Directory.CreateDirectory(this.directory);
                    this.logger.Information("Created config directory: {Directory}", this.directory);
                }
                return true;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to initialize file config persistence");
                return false;
            }
        }
    }
}
