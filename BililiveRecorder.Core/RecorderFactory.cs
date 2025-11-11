using System;
using BililiveRecorder.Core.Config;
using BililiveRecorder.Core.Config.Persistence;
using BililiveRecorder.Core.Config.V3;
using Serilog;
#if NET8_0_OR_GREATER
using BililiveRecorder.Core.Config.Persistence.Database;
#endif

#nullable enable
namespace BililiveRecorder.Core
{
    /// <summary>
    /// Factory for creating Recorder instances with different config persistence backends
    /// </summary>
    internal static class RecorderFactory
    {
        /// <summary>
        /// Create a recorder with file-based config persistence (default behavior)
        /// </summary>
        internal static IRecorder CreateWithFileConfig(string workDirectory, IRoomFactory roomFactory, ILogger logger)
        {
            logger.Information("Using file-based config persistence from: {WorkDirectory}", workDirectory);

            var persistence = new FileConfigPersistence(workDirectory, logger);
            var configParser = new ConfigParserV2(persistence, logger);

            var config = configParser.Load() ?? new ConfigV3();
            config.Global.WorkDirectory = workDirectory;

            var recorder = new Recorder(roomFactory, config, logger);

            // Update the save method to use the new persistence
            return new RecorderWithPersistence(recorder, configParser);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Create a recorder with PostgreSQL-based config persistence
        /// </summary>
        /// <param name="connectionString">PostgreSQL connection string</param>
        /// <param name="roomFactory">Room factory</param>
        /// <param name="logger">Logger</param>
        /// <param name="migrateFromFile">If true, migrate existing config from file (if exists)</param>
        /// <param name="fileDirectory">Directory for file-based config (for migration)</param>
        internal static IRecorder CreateWithPostgreSqlConfig(
            string connectionString,
            IRoomFactory roomFactory,
            ILogger logger,
            bool migrateFromFile = false,
            string? fileDirectory = null)
        {
            logger.Information("Using PostgreSQL-based config persistence");

            var dbPersistence = new PostgreSqlConfigPersistence(connectionString, logger);

            // Initialize database
            if (!dbPersistence.Initialize())
            {
                throw new Exception("Failed to initialize PostgreSQL database for config persistence");
            }

            // Migrate from file if requested
            if (migrateFromFile && !string.IsNullOrEmpty(fileDirectory))
            {
                logger.Information("Attempting to migrate config from file ({FileDirectory}) to PostgreSQL database", fileDirectory);
                var filePersistence = new FileConfigPersistence(fileDirectory, logger);

                if (filePersistence.IsAvailable())
                {
                    logger.Information("Config file found, starting migration process");

                    // Create backup before migration
                    var fileConfig = filePersistence.Load();
                    if (fileConfig != null)
                    {
                        var backupPath = System.IO.Path.Combine(fileDirectory, $"config_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                        ConfigMigrationHelper.BackupToFile(fileConfig, backupPath, logger);
                        logger.Information("Config backup created at: {BackupPath}", backupPath);
                    }

                    // Perform migration
                    if (ConfigMigrationHelper.MigrateConfig(filePersistence, dbPersistence, logger))
                    {
                        logger.Information("Config migration from file to database completed successfully");
                    }
                    else
                    {
                        logger.Warning("Config migration from file to database failed, using database config");
                    }
                }
                else
                {
                    logger.Information("No config file found to migrate, using database config");
                }
            }

            var configParser = new ConfigParserV2(dbPersistence, logger);
            var config = configParser.Load() ?? new ConfigV3();

            var recorder = new Recorder(roomFactory, config, logger);
            return new RecorderWithPersistence(recorder, configParser);
        }
#endif

        /// <summary>
        /// Wrapper class that intercepts SaveConfig calls and redirects to the new persistence
        /// </summary>
        private class RecorderWithPersistence : IRecorder
        {
            private readonly Recorder recorder;
            private readonly ConfigParserV2 configParser;

            public RecorderWithPersistence(Recorder recorder, ConfigParserV2 configParser)
            {
                this.recorder = recorder;
                this.configParser = configParser;
            }

            // Intercept SaveConfig to use new persistence
            public void SaveConfig()
            {
                this.configParser.Save(this.recorder.Config);
            }

            // Delegate all other members to the underlying recorder
            public ConfigV3 Config => this.recorder.Config;
            public System.Collections.ObjectModel.ReadOnlyObservableCollection<IRoom> Rooms => this.recorder.Rooms;

            public IRoom AddRoom(long roomid) => this.recorder.AddRoom(roomid);
            public IRoom AddRoom(long roomid, bool enabled) => this.recorder.AddRoom(roomid, enabled);
            public IRoom AddRoom(string roomUrl) => this.recorder.AddRoom(roomUrl);
            public IRoom AddRoom(string roomUrl, bool enabled) => this.recorder.AddRoom(roomUrl, enabled);
            public void RemoveRoom(IRoom room) => this.recorder.RemoveRoom(room);

            public event EventHandler<Event.AggregatedRoomEventArgs<Event.RecordSessionStartedEventArgs>>? RecordSessionStarted
            {
                add => this.recorder.RecordSessionStarted += value;
                remove => this.recorder.RecordSessionStarted -= value;
            }

            public event EventHandler<Event.AggregatedRoomEventArgs<Event.RecordSessionEndedEventArgs>>? RecordSessionEnded
            {
                add => this.recorder.RecordSessionEnded += value;
                remove => this.recorder.RecordSessionEnded -= value;
            }

            public event EventHandler<Event.AggregatedRoomEventArgs<Event.RecordFileOpeningEventArgs>>? RecordFileOpening
            {
                add => this.recorder.RecordFileOpening += value;
                remove => this.recorder.RecordFileOpening -= value;
            }

            public event EventHandler<Event.AggregatedRoomEventArgs<Event.RecordFileClosedEventArgs>>? RecordFileClosed
            {
                add => this.recorder.RecordFileClosed += value;
                remove => this.recorder.RecordFileClosed -= value;
            }

            public event EventHandler<Event.AggregatedRoomEventArgs<Event.IOStatsEventArgs>>? IOStats
            {
                add => this.recorder.IOStats += value;
                remove => this.recorder.IOStats -= value;
            }

            public event EventHandler<Event.AggregatedRoomEventArgs<Event.RecordingStatsEventArgs>>? RecordingStats
            {
                add => this.recorder.RecordingStats += value;
                remove => this.recorder.RecordingStats -= value;
            }

            public event EventHandler<IRoom>? StreamStarted
            {
                add => this.recorder.StreamStarted += value;
                remove => this.recorder.StreamStarted -= value;
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
            {
                add => this.recorder.PropertyChanged += value;
                remove => this.recorder.PropertyChanged -= value;
            }

            public void Dispose() => this.recorder.Dispose();
        }
    }
}
